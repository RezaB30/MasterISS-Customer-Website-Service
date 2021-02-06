using RadiusR.API.CustomerWebService.Requests.PartnerRequests;
using RadiusR.API.CustomerWebService.Responses.PartnerResponses;
using RadiusR.DB;
using RadiusR.DB.Utilities.Billing;
using RadiusR.SMS;
using RadiusR.SystemLogs;
using RezaB.API.WebService;
using RezaB.API.WebService.NLogExtentions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;

namespace RadiusR.API.CustomerWebService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "PartnerService" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select PartnerService.svc or PartnerService.svc.cs at the Solution Explorer and start debugging.
    public class PartnerService : GenericCustomerService, IPartnerService
    {
        WebServiceLogger Errorslogger = new WebServiceLogger("PartnerErrors");
        WebServiceLogger InComingInfoLogger = new WebServiceLogger("PartnerInComingInfo");
        public PartnerServicePaymentResponse PayBills(PartnerServicePaymentRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
                {
                    return new PartnerServicePaymentResponse(passwordHash, request)
                    {
                        PaymentResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }
                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.Username);
                    var dbSubUser = dbPartner.PartnerSubUsers.FirstOrDefault(su => su.Name == request.PaymentRequest.SubUserEmail);

                    if (!dbPartner.PartnerPermissions.Any(pp => pp.Permission == (short)RadiusR.DB.Enums.PartnerPermissions.Payment))
                    {
                        return new PartnerServicePaymentResponse(passwordHash, request)
                        {
                            PaymentResponse = null,
                            ResponseMessage = CommonResponse.PaymentPermissionNotFound(request.Culture)
                        };
                    }
                    if (request.PaymentRequest.BillIDs == null || request.PaymentRequest.BillIDs.Count() <= 0)
                    {
                        return new PartnerServicePaymentResponse(passwordHash, request)
                        {
                            PaymentResponse = null,
                            ResponseMessage = CommonResponse.BillsNotFoundException(request.Culture)
                        };
                    }
                    /*
                    .Include(bill => bill.Subscription)
                        .Include(bill => bill.BillFees.Select(bf => bf.Discount))
                     */
                    var dbBills = db.Bills.Where(bill => request.PaymentRequest.BillIDs.Contains(bill.ID) && bill.PaymentTypeID == (short)RadiusR.DB.Enums.PaymentType.None).ToArray();

                    if (dbBills.Count() != request.PaymentRequest.BillIDs.Count())
                    {
                        return new PartnerServicePaymentResponse(passwordHash, request)
                        {
                            PaymentResponse = null,
                            ResponseMessage = CommonResponse.BillsNotFoundException(request.Culture)
                        };
                    }
                    // pay bills
                    var paymentResults = db.PayBills(dbBills, DB.Enums.PaymentType.Partner, BillPayment.AccountantType.Admin, gateway: new BillPaymentGateway()
                    {
                        PaymentPartner = dbPartner,
                        PaymentPartnerSubUser = dbSubUser
                    });

                    if (paymentResults != BillPayment.ResponseType.Success)
                    {
                        if (paymentResults == BillPayment.ResponseType.NotEnoughCredit)
                        {
                            return new PartnerServicePaymentResponse(passwordHash, request)
                            {
                                PaymentResponse = null,
                                ResponseMessage = CommonResponse.NotEnoughCredit(request.Culture)
                            };
                        }
                        return new PartnerServicePaymentResponse(passwordHash, request)
                        {
                            PaymentResponse = null,
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture)
                        };
                    }

                    // set system logs
                    var gatewayName = dbPartner.Title + (dbSubUser != null ? " (" + dbSubUser.Name + ")" : string.Empty);
                    var SMSClient = new SMSService();

                    var billGroups = dbBills.GroupBy(bill => bill.Subscription).ToArray();
                    foreach (var group in billGroups)
                    {
                        db.SystemLogs.Add(SystemLogProcessor.BillPayment(group.Select(bill => bill.ID), null, group.Key.ID, RadiusR.DB.Enums.SystemLogInterface.PartnerWebService, request.Username, RadiusR.DB.Enums.PaymentType.Partner, gatewayName));
                        // send SMS
                        db.SMSArchives.Add(SMSClient.SendSubscriberSMS(group.Key, RadiusR.DB.Enums.SMSType.PaymentDone, new Dictionary<string, object>()
                        {
                            { SMSParamaterRepository.SMSParameterNameCollection.BillTotal, group.Sum(bill => bill.GetPayableCost()) }
                        }));
                    }

                    db.SaveChanges();
                    //dbBills.Select(b => b.ID).ToArray()
                    return new PartnerServicePaymentResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        PaymentResponse = dbBills.Select(b => b.ID).ToArray()
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServicePaymentResponse(passwordHash, request)
                {
                    PaymentResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Username)
                };
            }
        }
        public PartnerServiceAuthenticationResponse Authenticate(PartnerServiceAuthenticationRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
                {
                    return new PartnerServiceAuthenticationResponse(passwordHash, request)
                    {
                        AuthenticationResponse = new AuthenticationResponse()
                        {
                            IsAuthenticated = false
                        },
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.AuthenticationParameters.PartnerUsername);

                    if (!dbPartner.IsActive)
                    {
                        return new PartnerServiceAuthenticationResponse(passwordHash, request)
                        {
                            AuthenticationResponse = new AuthenticationResponse()
                            {
                                IsAuthenticated = false
                            },
                            ResponseMessage = CommonResponse.PartnerIsNotActive(request.Culture)
                        };
                    }

                    var partnerPasswordHash = dbPartner.Password;
                    if (!string.IsNullOrEmpty(request.AuthenticationParameters.SubUserEmail))
                    {
                        var dbSubUser = dbPartner.PartnerSubUsers.FirstOrDefault(psu => psu.Email == request.AuthenticationParameters.SubUserEmail);
                        if (dbSubUser == null)
                        {
                            return new PartnerServiceAuthenticationResponse(passwordHash, request)
                            {
                                AuthenticationResponse = new AuthenticationResponse()
                                {
                                    IsAuthenticated = false,
                                },
                                ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture)
                            };
                        }
                        partnerPasswordHash = dbSubUser.Password;
                    }
                    if (partnerPasswordHash.ToLower() != request.AuthenticationParameters.PartnerPasswordHash.ToLower())
                    {
                        Errorslogger.LogException(request.Username, new Exception("Wrong passwordHash"));
                        return new PartnerServiceAuthenticationResponse(passwordHash, request)
                        {
                            AuthenticationResponse = new AuthenticationResponse()
                            {
                                IsAuthenticated = false
                            },
                            ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                        };
                    }
                    return new PartnerServiceAuthenticationResponse(passwordHash, request)
                    {
                        AuthenticationResponse = new AuthenticationResponse()
                        {
                            Permissions = dbPartner.PartnerPermissions.Select(pp => new AuthenticationResponse.PermissionResult(pp.Permission, request.Culture)).ToArray(),
                            IsAuthenticated = true,
                            UserID = dbPartner.ID,
                            DisplayName = dbPartner.Title,
                            SetupServiceUser = dbPartner.CustomerSetupUser?.Username,
                            SetupServiceHash = dbPartner.CustomerSetupUser?.Password,
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceAuthenticationResponse(passwordHash, request)
                {
                    AuthenticationResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
    }
}

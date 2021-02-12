using RadiusR.API.CustomerWebService.Requests.PartnerRequests;
using RadiusR.API.CustomerWebService.Responses.PartnerResponses;
using RadiusR.DB;
using RadiusR.DB.Enums;
using RadiusR.DB.ModelExtentions;
using RadiusR.DB.QueryExtentions;
using RadiusR.DB.Utilities.Billing;
using RadiusR.DB.Utilities.ComplexOperations.Subscriptions.Registration;
using RadiusR.SMS;
using RadiusR.SystemLogs;
using RezaB.API.WebService;
using RezaB.API.WebService.NLogExtentions;
using RezaB.Data.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;

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
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServicePaymentResponse(passwordHash, request)
                    {
                        PaymentResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }
                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.PaymentRequest.UserEmail);
                    var dbSubUser = dbPartner.PartnerSubUsers.FirstOrDefault(su => su.Email == request.PaymentRequest.SubUserEmail);

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
                        db.SystemLogs.Add(SystemLogProcessor.BillPayment(group.Select(bill => bill.ID), null, group.Key.ID, RadiusR.DB.Enums.SystemLogInterface.PartnerWebService, request.PaymentRequest.UserEmail, RadiusR.DB.Enums.PaymentType.Partner, gatewayName));
                        // send SMS
                        db.SMSArchives.AddSafely(SMSClient.SendSubscriberSMS(group.Key, RadiusR.DB.Enums.SMSType.PaymentDone, new Dictionary<string, object>()
                        {
                            { SMSParamaterRepository.SMSParameterNameCollection.BillTotal,group.Sum(bill => bill.GetPayableCost()) }
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
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
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
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.AuthenticationParameters.UserEmail);

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
                    if (!string.IsNullOrEmpty(request.AuthenticationParameters.SubUserEmail) && request.AuthenticationParameters.UserEmail != request.AuthenticationParameters.SubUserEmail)
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
                            Permissions = dbPartner.PartnerPermissions.Select(pp => new AuthenticationResponse.PermissionResult(pp.Permission)).ToArray(),
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
        public PartnerServiceAddSubUserResponse AddSubUser(PartnerServiceAddSubUserRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceAddSubUserResponse(passwordHash, request)
                    {
                        AddSubUserResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                //if (string.IsNullOrEmpty(request.AddSubUserRequestParameters.SubUserEmail))
                //{
                //    return new PartnerServiceAddSubUserResponse(passwordHash, request)
                //    {
                //        AddSubUserResponse = null,
                //        ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                //    };
                //}

                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.AddSubUserRequestParameters.UserEmail);
                    if (dbPartner == null)
                    {
                        return new PartnerServiceAddSubUserResponse(passwordHash, request)
                        {
                            AddSubUserResponse = null,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    if (request.AddSubUserRequestParameters.UserEmail != request.AddSubUserRequestParameters.SubUserEmail)
                    {
                        return new PartnerServiceAddSubUserResponse(passwordHash, request)
                        {
                            AddSubUserResponse = null,
                            ResponseMessage = CommonResponse.PartnerNoPermissionResponse(request.Culture)
                        };
                    }
                    var activeSubUserCount = dbPartner.PartnerSubUsers.Count(su => su.IsActive);
                    if (activeSubUserCount >= dbPartner.MaxActiveUsers)
                    {
                        //MaxSubUsersReached
                        return new PartnerServiceAddSubUserResponse(passwordHash, request)
                        {
                            AddSubUserResponse = null,
                            ResponseMessage = CommonResponse.PartnerMaxSubUsersReachedResponse(request.Culture)
                        };
                    }
                    if (dbPartner.PartnerSubUsers.Any(su => su.Email.ToLower() == request.AddSubUserRequestParameters.RequestedSubUserEmail.ToLower()))
                    {
                        //SubUserExists
                        return new PartnerServiceAddSubUserResponse(passwordHash, request)
                        {
                            AddSubUserResponse = null,
                            ResponseMessage = CommonResponse.PartnerSubUserExistsResponse(request.Culture)
                        };
                    }
                    List<ValidationElement> validationElements = new List<ValidationElement>();
                    // validation
                    // SubUserEmail
                    {
                        var validationMessage = Validator.Required(request.AddSubUserRequestParameters.RequestedSubUserEmail, "request.AddSubUserRequestParameters.RequestedSubUserEmail");
                        if (validationMessage != null)
                        {
                            validationElements.Add(validationMessage);
                        }
                        else
                        {
                            validationMessage = Validator.MaxLength(request.AddSubUserRequestParameters.RequestedSubUserEmail, 300, "request.AddSubUserRequestParameters.RequestedSubUserEmail");
                            if (validationMessage != null)
                            {
                                validationElements.Add(validationMessage);
                            }
                            else
                            {
                                validationMessage = Validator.ValidateEmail(request.AddSubUserRequestParameters.RequestedSubUserEmail, "request.AddSubUserRequestParameters.RequestedSubUserEmail");
                                if (validationMessage != null)
                                {
                                    validationElements.Add(validationMessage);
                                }
                            }
                        }
                    }
                    // SubUserName
                    {
                        var validationMessage = Validator.Required(request.AddSubUserRequestParameters.RequestedSubUserName, "request.AddSubUserRequestParameters.RequestedSubUserName");
                        if (validationMessage != null)
                        {
                            validationElements.Add(validationMessage);
                        }
                    }
                    // SubUserPassword
                    {
                        var validationMessage = Validator.Required(request.AddSubUserRequestParameters.RequestedSubUserPassword, "request.AddSubUserRequestParameters.RequestedSubUserPassword");
                        if (validationMessage != null)
                        {
                            validationElements.Add(validationMessage);
                        }
                    }
                    if (validationElements.Any())
                    {
                        return new PartnerServiceAddSubUserResponse(passwordHash, request)
                        {
                            AddSubUserResponse = new AddSubUserResponse()
                            {
                                ValidationElements = validationElements.ToArray()
                            },
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture)
                        };
                    }
                    dbPartner.PartnerSubUsers.Add(new PartnerSubUser()
                    {
                        IsActive = true,
                        Name = request.AddSubUserRequestParameters.RequestedSubUserName,
                        Email = request.AddSubUserRequestParameters.RequestedSubUserEmail,
                        Password = request.AddSubUserRequestParameters.RequestedSubUserPassword
                    });
                    db.SaveChanges();
                    return new PartnerServiceAddSubUserResponse(passwordHash, request)
                    {
                        AddSubUserResponse = new AddSubUserResponse()
                        {
                            RequestedSubUserEmail = request.AddSubUserRequestParameters.RequestedSubUserEmail
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceAddSubUserResponse(passwordHash, request);
            }
        }
        public PartnerServiceSubUserResponse DisableSubUser(PartnerServiceSubUserRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSubUserResponse(passwordHash, request)
                    {
                        SubUserResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                //if (!string.IsNullOrEmpty(request.SubUserEmail))
                //{
                //    return (SubUserResponse)SendResponse(new SubUserResponse(ResponseCodes.NoPermission, request.Culture, request.Username, request.SubUserEmail, null));
                //}

                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.SubUserRequest.UserEmail);
                    if (dbPartner == null)
                    {
                        return new PartnerServiceSubUserResponse(passwordHash, request)
                        {
                            SubUserResponse = null,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    if (request.SubUserRequest.UserEmail != request.SubUserRequest.SubUserEmail)
                    {
                        return new PartnerServiceSubUserResponse(passwordHash, request)
                        {
                            SubUserResponse = null,
                            ResponseMessage = CommonResponse.PartnerNoPermissionResponse(request.Culture)
                        };
                    }
                    var dbSubUser = dbPartner.PartnerSubUsers.FirstOrDefault(su => su.Email == request.SubUserRequest.RequestedSubUserEmail);

                    if (dbSubUser == null)
                    {
                        return new PartnerServiceSubUserResponse(passwordHash, request)
                        {
                            SubUserResponse = null,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    if (dbSubUser.IsActive)
                    {
                        dbSubUser.IsActive = false;
                        db.SaveChanges();
                    }
                    return new PartnerServiceSubUserResponse(passwordHash, request)
                    {
                        SubUserResponse = new SubUserResponse()
                        {
                            RequestedSubUserEmail = request.SubUserRequest.RequestedSubUserEmail
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSubUserResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    SubUserResponse = null
                };
            }
        }
        public PartnerServiceBillListResponse BillsBySubscriberNo(PartnerServiceBillListRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceBillListResponse(passwordHash, request)
                    {
                        BillListResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.BillListRequest.UserEmail);
                    var dbSubUser = dbPartner.PartnerSubUsers.FirstOrDefault(su => su.Email == request.BillListRequest.SubUserEmail);

                    if (!dbPartner.PartnerPermissions.Any(pp => pp.Permission == (short)RadiusR.DB.Enums.PartnerPermissions.Payment))
                    {
                        return new PartnerServiceBillListResponse(passwordHash, request)
                        {
                            BillListResponse = null,
                            ResponseMessage = CommonResponse.PaymentPermissionNotFound(request.Culture)
                        };
                    }

                    var dbSubscriber = db.Subscriptions.FirstOrDefault(s => s.SubscriberNo == request.BillListRequest.SubscriberNo);

                    if (dbSubscriber == null)
                    {
                        return new PartnerServiceBillListResponse(passwordHash, request)
                        {
                            BillListResponse = null,
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture)
                        };
                    }

                    var bills = db.Bills.Where(b => b.SubscriptionID == dbSubscriber.ID && b.PaymentTypeID == (short)RadiusR.DB.Enums.PaymentType.None).OrderBy(b => b.ID).ToArray();
                    var results = bills.Select(b => new BillListResponse.BillInfo()
                    {
                        ID = b.ID,
                        IssueDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(b.IssueDate),
                        DueDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(b.DueDate),
                        Total = b.GetPayableCost()
                    }).ToArray();

                    var totalCredits = dbSubscriber.SubscriptionCredits.Select(sc => sc.Amount).DefaultIfEmpty(0m).Sum();
                    return new PartnerServiceBillListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        BillListResponse = new BillListResponse()
                        {
                            Bills = results,
                            SubscriberName = dbSubscriber.ValidDisplayName,
                            TotalCredits = totalCredits
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceBillListResponse(passwordHash, request)
                {
                    BillListResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public PartnerServiceSubUserResponse EnableSubUser(PartnerServiceSubUserRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSubUserResponse(passwordHash, request)
                    {
                        SubUserResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }
                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.SubUserRequest.UserEmail);
                    if (dbPartner == null)
                    {
                        return new PartnerServiceSubUserResponse(passwordHash, request)
                        {
                            SubUserResponse = null,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    if (request.SubUserRequest.UserEmail != request.SubUserRequest.SubUserEmail)
                    {
                        return new PartnerServiceSubUserResponse(passwordHash, request)
                        {
                            SubUserResponse = null,
                            ResponseMessage = CommonResponse.PartnerNoPermissionResponse(request.Culture)
                        };
                    }
                    var activeSubUserCount = dbPartner.PartnerSubUsers.Count(su => su.IsActive);
                    if (activeSubUserCount >= dbPartner.MaxActiveUsers)
                    {
                        return new PartnerServiceSubUserResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerMaxSubUsersReachedResponse(request.Culture),
                            SubUserResponse = null
                        };
                    }
                    var dbSubUser = dbPartner.PartnerSubUsers.FirstOrDefault(su => su.Email == request.SubUserRequest.RequestedSubUserEmail);

                    if (dbSubUser == null)
                    {
                        return new PartnerServiceSubUserResponse(passwordHash, request)
                        {
                            SubUserResponse = null,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    if (!dbSubUser.IsActive)
                    {
                        dbSubUser.IsActive = true;
                        db.SaveChanges();
                    }
                    return new PartnerServiceSubUserResponse(passwordHash, request)
                    {
                        SubUserResponse = new SubUserResponse()
                        {
                            RequestedSubUserEmail = request.SubUserRequest.RequestedSubUserEmail
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSubUserResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    SubUserResponse = null
                };
            }
        }

        public PartnerServiceKeyValueListResponse GetCultures(PartnerServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }
                var results = new KeyValueItem[] { new KeyValueItem() { Key = 0, Value = "en-US" }, new KeyValueItem() { Key = 0, Value = "tr-tr" } };
                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public PartnerServiceKeyValueListResponse GetTCKTypes(PartnerServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var list = new LocalizedList<RadiusR.DB.Enums.IDCardTypes, RadiusR.Localization.Lists.IDCardTypes>();
                var results = list.GetList(CreateCulture(request.Culture)).Select(item => new KeyValueItem() { Key = item.Key, Value = item.Value }).ToArray();

                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public PartnerServiceKeyValueListResponse GetCustomerTypes(PartnerServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var list = new LocalizedList<RadiusR.DB.Enums.CustomerType, RadiusR.Localization.Lists.CustomerType>();
                var results = list.GetList(CreateCulture(request.Culture)).Select(item => new KeyValueItem() { Key = item.Key, Value = item.Value }).ToArray();

                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public PartnerServiceKeyValueListResponse GetSexes(PartnerServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var list = new LocalizedList<RadiusR.DB.Enums.Sexes, RadiusR.Localization.Lists.Sexes>();
                var results = list.GetList(CreateCulture(request.Culture)).Select(item => new KeyValueItem() { Key = item.Key, Value = item.Value }).ToArray();

                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public PartnerServiceKeyValueListResponse GetNationalities(PartnerServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var list = new LocalizedList<RadiusR.DB.Enums.CountryCodes, RadiusR.Localization.Lists.CountryCodes>();
                var results = list.GetList(CreateCulture(request.Culture)).Select(item => new KeyValueItem() { Key = item.Key, Value = item.Value }).ToArray();

                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public PartnerServiceKeyValueListResponse GetProfessions(PartnerServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var list = new LocalizedList<RadiusR.DB.Enums.Profession, RadiusR.Localization.Lists.Profession>();
                var results = list.GetList(CreateCulture(request.Culture)).Select(item => new KeyValueItem() { Key = item.Key, Value = item.Value }).ToArray();

                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public PartnerServiceKeyValueListResponse GetPartnerTariffs(PartnerServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                using (RadiusREntities db = new RadiusREntities())
                {
                    //Include(p => p.PartnerGroup.PartnerAvailableTariffs.Select(pat => pat.Service.Domains)).
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.ParameterlessRequest.UserEmail);

                    if (!dbPartner.PartnerPermissions.Any(pp => pp.Permission == (short)RadiusR.DB.Enums.PartnerPermissions.Sale))
                    {
                        return new PartnerServiceKeyValueListResponse(passwordHash, request)
                        {
                            KeyValueItemResponse = null,
                            ResponseMessage = CommonResponse.PartnerNoPermissionResponse(request.Culture)
                        };
                    }
                    var rawResults = dbPartner.PartnerGroup.PartnerAvailableTariffs.Select(pat => new { ID = pat.ID, TariffName = pat.Service.Name, DomainName = pat.Domain.Name }).ToArray();
                    var list = new LocalizedList<RadiusR.DB.Enums.CommitmentLength, RadiusR.Localization.Lists.CommitmentLength>();
                    var results = rawResults.Select(rr => new KeyValueItem()
                    {
                        Key = rr.ID,
                        Value = rr.TariffName + "(" + rr.DomainName + ")"
                    }).ToArray();

                    return new PartnerServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = results,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public PartnerServiceKeyValueListResponse GetPaymentDays(PartnerServiceListFromIDRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.ListFromIDRequest.UserEmail);

                    if (!dbPartner.PartnerPermissions.Any(pp => pp.Permission == (short)RadiusR.DB.Enums.PartnerPermissions.Sale))
                    {
                        return new PartnerServiceKeyValueListResponse(passwordHash, request)
                        {
                            KeyValueItemResponse = null,
                            ResponseMessage = CommonResponse.PartnerNoPermissionResponse(request.Culture)
                        };
                    }
                    var results = new KeyValueItem[0];
                    var availableTariff = dbPartner.PartnerGroup.PartnerAvailableTariffs.FirstOrDefault(pat => pat.ID == request.ListFromIDRequest.ID);
                    if (availableTariff != null)
                    {
                        results = availableTariff.Service.ServiceBillingPeriods.Select(sbp => new KeyValueItem() { Key = sbp.DayOfMonth, Value = sbp.DayOfMonth.ToString() }).ToArray();
                    }
                    return new PartnerServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = results,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public PartnerServiceCreditReportResponse GetCreditReport(PartnerServiceCreditReportRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceCreditReportResponse(passwordHash, request)
                    {
                        CreditReportResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }
                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.CreditReportRequest.UserEmail);

                    if (!dbPartner.PartnerPermissions.Any(pp => pp.Permission == (short)RadiusR.DB.Enums.PartnerPermissions.Payment))
                    {
                        return new PartnerServiceCreditReportResponse(passwordHash, request)
                        {
                            CreditReportResponse = null,
                            ResponseMessage = CommonResponse.PaymentPermissionNotFound(request.Culture)
                        };
                    }

                    var total = db.PartnerCredits.Where(pc => pc.PartnerID == dbPartner.ID).Select(pc => pc.Amount).DefaultIfEmpty(0m).Sum();
                    CreditChangeItem[] details = null;
                    if (request.CreditReportRequest.WithDetails == true)
                    {
                        details = db.PartnerCredits.Where(pc => pc.PartnerID == dbPartner.ID).OrderByDescending(pc => pc.Date).Take(100).Select(pc => new CreditChangeItem()
                        {
                            Amount = pc.Amount,
                            Date = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(pc.Date),
                            Details = pc.Details
                        }).ToArray();
                    }
                    return new PartnerServiceCreditReportResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        CreditReportResponse = new CreditReportResponse()
                        {
                            Total = total,
                            Details = details
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceCreditReportResponse(passwordHash, request)
                {
                    CreditReportResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public PartnerServiceSMSCodeResponse SendConfirmationSMS(PartnerServiceSMSCodeRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSMSCodeResponse(passwordHash, request)
                    {
                        SMSCodeResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var phoneNoRegex = new Regex(@"^[1-9]{1}[0-9]{9}$", RegexOptions.ECMAScript);
                if (request.SMSCodeRequest.PhoneNo == null || !phoneNoRegex.IsMatch(request.SMSCodeRequest.PhoneNo))
                {
                    return new PartnerServiceSMSCodeResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.PartnerInvalidPhoneNoResponse(request.Culture),
                        SMSCodeResponse = null
                    };
                }

                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.SMSCodeRequest.UserEmail);

                    if (!dbPartner.PartnerPermissions.Any(pp => pp.Permission == (short)RadiusR.DB.Enums.PartnerPermissions.Sale))
                    {
                        return new PartnerServiceSMSCodeResponse(passwordHash, request)
                        {
                            SMSCodeResponse = null,
                            ResponseMessage = CommonResponse.PartnerNoPermissionResponse(request.Culture)
                        };
                    }

                    var code = new Random().Next(100000, 1000000).ToString("000000");
                    SMSService smsClient = new SMSService();
                    smsClient.SendGenericSMS(request.SMSCodeRequest.PhoneNo, request.Culture, RadiusR.DB.Enums.SMSType.OperationCode, new Dictionary<string, object>() { { SMSParamaterRepository.SMSParameterNameCollection.SMSCode, code } });
                    return new PartnerServiceSMSCodeResponse(passwordHash, request)
                    {
                        SMSCodeResponse = new SMSCodeResponse()
                        {
                            Code = code
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSMSCodeResponse(passwordHash, request)
                {
                    SMSCodeResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public PartnerServiceNewCustomerRegisterResponse NewCustomerRegister(PartnerServiceNewCustomerRegisterRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                        NewCustomerRegisterResponse = null
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    //var specialOfferId = db.SpecialOffers.Where(s => s.IsReferral == true && DateTime.Now > s.StartDate && DateTime.Now < s.EndDate).ToList();
                    //if (specialOfferId.Count != 1)
                    //{
                    //    return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                    //    {
                    //        NewCustomerRegisterResponse = null,
                    //        ResponseMessage = CommonResponse.SpecialOfferError(request.Culture)
                    //    };
                    //}
                    var externalTariff = db.ExternalTariffs.GetActiveExternalTariffs().FirstOrDefault(ext => ext.TariffID == request.CustomerRegisterParameters.SubscriptionInfo.ServiceID);
                    if (externalTariff == null)
                    {
                        return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.TariffNotFound(request.Culture),
                            NewCustomerRegisterResponse = null
                        };
                    }
                    var billingPeriod = externalTariff.Service.GetBestBillingPeriod(DateTime.Now.Day);
                    //var currentSpecialOfferId = specialOfferId.FirstOrDefault().ID;
                    var registeredCustomer = new Customer();
                    var register = request.CustomerRegisterParameters;
                    CustomerRegistrationInfo registrationInfo = new CustomerRegistrationInfo()
                    {
                        CorporateInfo = register.CorporateCustomerInfo == null ? null : new CustomerRegistrationInfo.CorporateCustomerInfo()
                        {
                            CentralSystemNo = register.CorporateCustomerInfo.CentralSystemNo,
                            CompanyAddress = register.CorporateCustomerInfo.CompanyAddress == null ? null : new CustomerRegistrationInfo.AddressInfo()
                            {
                                AddressNo = register.CorporateCustomerInfo.CompanyAddress.AddressNo,
                                AddressText = register.CorporateCustomerInfo.CompanyAddress.AddressText,
                                ApartmentID = register.CorporateCustomerInfo.CompanyAddress.ApartmentID,
                                ApartmentNo = register.CorporateCustomerInfo.CompanyAddress.ApartmentNo,
                                DistrictID = register.CorporateCustomerInfo.CompanyAddress.DistrictID,
                                DistrictName = register.CorporateCustomerInfo.CompanyAddress.DistrictName,
                                DoorID = register.CorporateCustomerInfo.CompanyAddress.DoorID,
                                DoorNo = register.CorporateCustomerInfo.CompanyAddress.DoorNo,
                                Floor = register.CorporateCustomerInfo.CompanyAddress.Floor,
                                NeighbourhoodID = register.CorporateCustomerInfo.CompanyAddress.NeighbourhoodID,
                                NeighbourhoodName = register.CorporateCustomerInfo.CompanyAddress.NeighbourhoodName,
                                PostalCode = register.CorporateCustomerInfo.CompanyAddress.PostalCode,
                                ProvinceID = register.CorporateCustomerInfo.CompanyAddress.ProvinceID,
                                ProvinceName = register.CorporateCustomerInfo.CompanyAddress.ProvinceName,
                                RuralCode = register.CorporateCustomerInfo.CompanyAddress.RuralCode,
                                StreetID = register.CorporateCustomerInfo.CompanyAddress.StreetID,
                                StreetName = register.CorporateCustomerInfo.CompanyAddress.StreetName
                            },
                            ExecutiveBirthPlace = register.CorporateCustomerInfo.ExecutiveBirthPlace,
                            ExecutiveFathersName = register.CorporateCustomerInfo.ExecutiveFathersName,
                            ExecutiveMothersMaidenName = register.CorporateCustomerInfo.ExecutiveMothersMaidenName,
                            ExecutiveMothersName = register.CorporateCustomerInfo.ExecutiveMothersName,
                            ExecutiveNationality = (CountryCodes?)register.CorporateCustomerInfo.ExecutiveNationality,
                            ExecutiveProfession = (Profession?)register.CorporateCustomerInfo.ExecutiveProfession,
                            ExecutiveResidencyAddress = register.CorporateCustomerInfo.ExecutiveResidencyAddress == null ? null : new CustomerRegistrationInfo.AddressInfo()
                            {
                                AddressNo = register.CorporateCustomerInfo.ExecutiveResidencyAddress.AddressNo,
                                AddressText = register.CorporateCustomerInfo.ExecutiveResidencyAddress.AddressText,
                                ApartmentID = register.CorporateCustomerInfo.ExecutiveResidencyAddress.ApartmentID,
                                ApartmentNo = register.CorporateCustomerInfo.ExecutiveResidencyAddress.ApartmentNo,
                                DistrictID = register.CorporateCustomerInfo.ExecutiveResidencyAddress.DistrictID,
                                DistrictName = register.CorporateCustomerInfo.ExecutiveResidencyAddress.DistrictName,
                                DoorID = register.CorporateCustomerInfo.ExecutiveResidencyAddress.DoorID,
                                DoorNo = register.CorporateCustomerInfo.ExecutiveResidencyAddress.DoorNo,
                                Floor = register.CorporateCustomerInfo.ExecutiveResidencyAddress.Floor,
                                NeighbourhoodID = register.CorporateCustomerInfo.ExecutiveResidencyAddress.NeighbourhoodID,
                                NeighbourhoodName = register.CorporateCustomerInfo.ExecutiveResidencyAddress.NeighbourhoodName,
                                PostalCode = register.CorporateCustomerInfo.ExecutiveResidencyAddress.PostalCode,
                                ProvinceID = register.CorporateCustomerInfo.ExecutiveResidencyAddress.ProvinceID,
                                ProvinceName = register.CorporateCustomerInfo.ExecutiveResidencyAddress.ProvinceName,
                                StreetID = register.CorporateCustomerInfo.ExecutiveResidencyAddress.StreetID,
                                StreetName = register.CorporateCustomerInfo.ExecutiveResidencyAddress.StreetName,
                                RuralCode = register.CorporateCustomerInfo.ExecutiveResidencyAddress.RuralCode,
                            },
                            ExecutiveSex = (Sexes?)register.CorporateCustomerInfo.ExecutiveSex,
                            TaxNo = register.CorporateCustomerInfo.TaxNo,
                            TaxOffice = register.CorporateCustomerInfo.TaxOffice,
                            Title = register.CorporateCustomerInfo.Title,
                            TradeRegistrationNo = register.CorporateCustomerInfo.TradeRegistrationNo
                        },
                        GeneralInfo = register.CustomerGeneralInfo == null ? null : new CustomerRegistrationInfo.CustomerGeneralInfo()
                        {
                            BillingAddress = register.CustomerGeneralInfo.BillingAddress == null ? null : new CustomerRegistrationInfo.AddressInfo()
                            {
                                AddressNo = register.CustomerGeneralInfo.BillingAddress.AddressNo,
                                AddressText = register.CustomerGeneralInfo.BillingAddress.AddressText,
                                ApartmentID = register.CustomerGeneralInfo.BillingAddress.ApartmentID,
                                ApartmentNo = register.CustomerGeneralInfo.BillingAddress.ApartmentNo,
                                DistrictID = register.CustomerGeneralInfo.BillingAddress.DistrictID,
                                DistrictName = register.CustomerGeneralInfo.BillingAddress.DistrictName,
                                DoorID = register.CustomerGeneralInfo.BillingAddress.DoorID,
                                DoorNo = register.CustomerGeneralInfo.BillingAddress.DoorNo,
                                Floor = register.CustomerGeneralInfo.BillingAddress.Floor,
                                NeighbourhoodID = register.CustomerGeneralInfo.BillingAddress.NeighbourhoodID,
                                NeighbourhoodName = register.CustomerGeneralInfo.BillingAddress.NeighbourhoodName,
                                PostalCode = register.CustomerGeneralInfo.BillingAddress.PostalCode,
                                ProvinceID = register.CustomerGeneralInfo.BillingAddress.ProvinceID,
                                ProvinceName = register.CustomerGeneralInfo.BillingAddress.ProvinceName,
                                RuralCode = register.CustomerGeneralInfo.BillingAddress.RuralCode,
                                StreetID = register.CustomerGeneralInfo.BillingAddress.StreetID,
                                StreetName = register.CustomerGeneralInfo.BillingAddress.StreetName
                            },
                            ContactPhoneNo = register.CustomerGeneralInfo.ContactPhoneNo,
                            Culture = register.CustomerGeneralInfo.Culture,
                            CustomerType = CustomerType.Individual,
                            Email = register.CustomerGeneralInfo.Email,
                            OtherPhoneNos = register.CustomerGeneralInfo.OtherPhoneNos == null ? null : register.CustomerGeneralInfo.OtherPhoneNos.Select(p => new CustomerRegistrationInfo.PhoneNoListItem()
                            {
                                Number = p.Number
                            })
                        },
                        IDCard = register.IDCardInfo == null ? null : new CustomerRegistrationInfo.IDCardInfo()
                        {
                            BirthDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.ParseDateTime(register.IDCardInfo.BirthDate),
                            CardType = (IDCardTypes?)register.IDCardInfo.CardType,
                            DateOfIssue = RezaB.API.WebService.DataTypes.ServiceTypeConverter.ParseDateTime(register.IDCardInfo.DateOfIssue),
                            District = register.IDCardInfo.District,
                            FirstName = register.IDCardInfo.FirstName,
                            LastName = register.IDCardInfo.LastName,
                            Neighbourhood = register.IDCardInfo.Neighbourhood,
                            PageNo = register.IDCardInfo.PageNo,
                            PassportNo = register.IDCardInfo.PassportNo,
                            PlaceOfIssue = register.IDCardInfo.PlaceOfIssue,
                            Province = register.IDCardInfo.Province,
                            RowNo = register.IDCardInfo.RowNo,
                            SerialNo = register.IDCardInfo.SerialNo,
                            TCKNo = register.IDCardInfo.TCKNo,
                            VolumeNo = register.IDCardInfo.VolumeNo
                        },
                        IndividualInfo = register.IndividualCustomerInfo == null ? null : new CustomerRegistrationInfo.IndividualCustomerInfo()
                        {
                            BirthPlace = register.IndividualCustomerInfo.BirthPlace,
                            FathersName = register.IndividualCustomerInfo.FathersName,
                            MothersMaidenName = register.IndividualCustomerInfo.MothersMaidenName,
                            MothersName = register.IndividualCustomerInfo.MothersName,
                            Nationality = (CountryCodes?)register.IndividualCustomerInfo.Nationality,
                            Profession = (Profession?)register.IndividualCustomerInfo.Profession,
                            ResidencyAddress = register.IndividualCustomerInfo.ResidencyAddress == null ? null : new CustomerRegistrationInfo.AddressInfo()
                            {
                                AddressNo = register.IndividualCustomerInfo.ResidencyAddress.AddressNo,
                                AddressText = register.IndividualCustomerInfo.ResidencyAddress.AddressText,
                                ApartmentID = register.IndividualCustomerInfo.ResidencyAddress.ApartmentID,
                                ApartmentNo = register.IndividualCustomerInfo.ResidencyAddress.ApartmentNo,
                                DistrictID = register.IndividualCustomerInfo.ResidencyAddress.DistrictID,
                                DistrictName = register.IndividualCustomerInfo.ResidencyAddress.DistrictName,
                                DoorID = register.IndividualCustomerInfo.ResidencyAddress.DoorID,
                                DoorNo = register.IndividualCustomerInfo.ResidencyAddress.DoorNo,
                                Floor = register.IndividualCustomerInfo.ResidencyAddress.Floor,
                                NeighbourhoodID = register.IndividualCustomerInfo.ResidencyAddress.NeighbourhoodID,
                                NeighbourhoodName = register.IndividualCustomerInfo.ResidencyAddress.NeighbourhoodName,
                                PostalCode = register.IndividualCustomerInfo.ResidencyAddress.PostalCode,
                                ProvinceID = register.IndividualCustomerInfo.ResidencyAddress.ProvinceID,
                                ProvinceName = register.IndividualCustomerInfo.ResidencyAddress.ProvinceName,
                                RuralCode = register.IndividualCustomerInfo.ResidencyAddress.RuralCode,
                                StreetID = register.IndividualCustomerInfo.ResidencyAddress.StreetID,
                                StreetName = register.IndividualCustomerInfo.ResidencyAddress.StreetName
                            },
                            Sex = (Sexes?)register.IndividualCustomerInfo.Sex
                        },
                        SubscriptionInfo = register.SubscriptionInfo == null ? null : new CustomerRegistrationInfo.SubscriptionRegistrationInfo()
                        {
                            DomainID = externalTariff.DomainID,
                            ServiceID = externalTariff.TariffID,
                            SetupAddress = new CustomerRegistrationInfo.AddressInfo()
                            {
                                AddressNo = register.SubscriptionInfo.SetupAddress.AddressNo,
                                AddressText = register.SubscriptionInfo.SetupAddress.AddressText,
                                ApartmentID = register.SubscriptionInfo.SetupAddress.ApartmentID,
                                ApartmentNo = register.SubscriptionInfo.SetupAddress.ApartmentNo,
                                DistrictID = register.SubscriptionInfo.SetupAddress.DistrictID,
                                DistrictName = register.SubscriptionInfo.SetupAddress.DistrictName,
                                DoorID = register.SubscriptionInfo.SetupAddress.DoorID,
                                DoorNo = register.SubscriptionInfo.SetupAddress.DoorNo,
                                Floor = register.SubscriptionInfo.SetupAddress.Floor,
                                NeighbourhoodID = register.SubscriptionInfo.SetupAddress.NeighbourhoodID,
                                NeighbourhoodName = register.SubscriptionInfo.SetupAddress.NeighbourhoodName,
                                PostalCode = register.SubscriptionInfo.SetupAddress.PostalCode,
                                ProvinceID = register.SubscriptionInfo.SetupAddress.ProvinceID,
                                ProvinceName = register.SubscriptionInfo.SetupAddress.ProvinceName,
                                RuralCode = register.SubscriptionInfo.SetupAddress.RuralCode,
                                StreetID = register.SubscriptionInfo.SetupAddress.StreetID,
                                StreetName = register.SubscriptionInfo.SetupAddress.StreetName
                            },
                            BillingPeriod = billingPeriod,
                            ReferralDiscount = null
                            //ReferralDiscount = string.IsNullOrEmpty(request.CustomerRegisterParameters.SubscriptionInfo.ReferralDiscountInfo.ReferenceNo) ? null : new CustomerRegistrationInfo.ReferralDiscountInfo()
                            //{
                            //    ReferenceNo = request.CustomerRegisterParameters.SubscriptionInfo.ReferralDiscountInfo.ReferenceNo,
                            //    SpecialOfferID = currentSpecialOfferId
                            //}
                        },
                    };
                    Dictionary<string, string> valuePairs = new Dictionary<string, string>();
                    // check for existing customer
                    var dbCustomer = db.Customers.FirstOrDefault(c => c.CustomerIDCard.TCKNo == request.CustomerRegisterParameters.IDCardInfo.TCKNo && c.CustomerType == request.CustomerRegisterParameters.CustomerGeneralInfo.CustomerType);
                    if (dbCustomer == null)
                    {
                        // create new customer
                        var result = RadiusR.DB.Utilities.ComplexOperations.Subscriptions.Registration.Registration.RegisterSubscriptionWithNewCustomer(db, registrationInfo, out registeredCustomer);
                        if (result != null)
                        {
                            var dic = result.ToDictionary(x => x.Key, x => x.ToArray());
                            foreach (var item in dic)
                            {
                                valuePairs.Add(item.Key, string.Join("-", item.Value));
                            }
                            return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                            {
                                NewCustomerRegisterResponse = valuePairs,
                                ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                            };
                        }
                    }
                    else
                    {
                        var result = RadiusR.DB.Utilities.ComplexOperations.Subscriptions.Registration.Registration.RegisterSubscriptionForExistingCustomer(db, registrationInfo.SubscriptionInfo, dbCustomer);
                        if (result != null)
                        {
                            var dic = result.ToDictionary(x => x.Key, x => x.ToArray());
                            foreach (var item in dic)
                            {
                                valuePairs.Add(item.Key, string.Join("-", item.Value));
                            }
                            return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                            {
                                NewCustomerRegisterResponse = valuePairs,
                                ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                            };
                        }
                    }
                    ////if (registeredCustomer == null)
                    ////{
                    ////    //exist customer
                    ////    //return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                    ////    //{
                    ////    //    ResponseMessage = CommonResponse.HaveAlreadyCustomer(request.Culture),
                    ////    //    NewCustomerRegisterResponse = null
                    ////    //};
                    ////}
                    //if (registeredCustomer != null)
                    //{
                    //    db.Customers.Add(registeredCustomer);
                    //}
                    db.SaveChanges();
                    db.SystemLogs.Add(RadiusR.SystemLogs.SystemLogProcessor.AddSubscription(null, registeredCustomer.Subscriptions.FirstOrDefault().ID, registeredCustomer.ID, SystemLogInterface.PartnerWebService, request.Username, registeredCustomer.Subscriptions.FirstOrDefault().SubscriberNo));
                    db.SaveChanges();
                    return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                    {
                        NewCustomerRegisterResponse = valuePairs,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                {
                    NewCustomerRegisterResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public PartnerServiceIDCardValidationResponse IDCardValidation(PartnerServiceIDCardValidationRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceIDCardValidationResponse(passwordHash, request)
                    {
                        IDCardValidationResponse = false,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                var validation = new RezaB.API.TCKValidation.TCKValidationClient();
                var BirthDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.ParseDate(request.IDCardValidationRequest.BirthDate);
                if (!BirthDate.HasValue)
                {
                    BirthDate = DateTime.Now;
                }
                if (request.IDCardValidationRequest.IDCardType == (int)RadiusR.DB.Enums.IDCardTypes.TCIDCardWithChip)
                {
                    var result = validation.ValidateNewTCK(
                        request.IDCardValidationRequest.TCKNo,
                        request.IDCardValidationRequest.FirstName,
                        request.IDCardValidationRequest.LastName,
                        BirthDate.Value,
                        request.IDCardValidationRequest.RegistirationNo);
                    return new PartnerServiceIDCardValidationResponse(passwordHash, request)
                    {
                        IDCardValidationResponse = result,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
                else
                {
                    if (request.IDCardValidationRequest.RegistirationNo.Length != 9)
                    {
                        return new PartnerServiceIDCardValidationResponse(passwordHash, request)
                        {
                            IDCardValidationResponse = false,
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.ErrorMessages.ResourceManager.GetString("InvalidSerialNo", CultureInfo.CreateSpecificCulture(request.Culture)))
                        };
                    }
                    var serial = request.IDCardValidationRequest.RegistirationNo.Substring(0, 3);
                    var serialNumber = request.IDCardValidationRequest.RegistirationNo.Substring(3, request.IDCardValidationRequest.RegistirationNo.Length - 3);
                    var result = validation.ValidateOldTCK(
                        request.IDCardValidationRequest.TCKNo,
                        request.IDCardValidationRequest.FirstName,
                        request.IDCardValidationRequest.LastName,
                        BirthDate.Value, serialNumber, serial);
                    return new PartnerServiceIDCardValidationResponse(passwordHash, request)
                    {
                        IDCardValidationResponse = result,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceIDCardValidationResponse(passwordHash, request)
                {
                    IDCardValidationResponse = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public PartnerServiceAllowanceDetailsResponse GetAllowanceDetails(PartnerServiceAllowanceRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
                    {
                        AllowanceDetailsResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                if (request.PartnerAllowanceRequest.AllowanceTypeId == null)
                {
                    return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, string.Format(CreateErrorMessage("Required", request.Culture), "AllowanceTypeId")),
                        AllowanceDetailsResponse = null
                    };
                }
                if (request.PartnerAllowanceRequest.PartnerId == null)
                {
                    return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, string.Format(CreateErrorMessage("Required", request.Culture), "PartnerId")),
                        AllowanceDetailsResponse = null
                    };
                }
                using (var db = new RadiusREntities())
                {                    
                    var allowances = db.GetAllowanceDetails(request.PartnerAllowanceRequest.PartnerId.Value, (PartnerCollectionType)request.PartnerAllowanceRequest.AllowanceTypeId);
                    var allowanceList = allowances.Select(a => new AllowanceDetailsResponse()
                    {
                        AllowanceStateID = (int)a.Key,
                        AllowanceStateName = new LocalizedList<PartnerAllowanceState, RadiusR.Localization.Lists.PartnerAllowanceState>().GetDisplayText((int)a.Key, CreateCulture(request.Culture)),
                        Price = a.Value
                    }).ToArray();
                    return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        AllowanceDetailsResponse = allowanceList
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
                {
                    AllowanceDetailsResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        //public object GetPartnerCollectionList(PartnerServiceAllowanceRequest request)
        //{
        //    var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
        //    var passwordHash = HashUtilities.GetHexString<SHA256>(password);
        //    try
        //    {
        //        InComingInfoLogger.LogIncomingMessage(request);
        //        if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
        //        {
        //            return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
        //            {
        //                AllowanceDetailsResponse = null,
        //                ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
        //            };
        //        }
        //        if (request.PartnerAllowanceRequest.AllowanceTypeId == null)
        //        {
        //            return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
        //            {
        //                ResponseMessage = CommonResponse.FailedResponse(request.Culture, string.Format(CreateErrorMessage("Required", request.Culture), "AllowanceTypeId")),
        //                AllowanceDetailsResponse = null
        //            };
        //        }
        //        if (request.PartnerAllowanceRequest.PartnerId == null)
        //        {
        //            return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
        //            {
        //                ResponseMessage = CommonResponse.FailedResponse(request.Culture, string.Format(CreateErrorMessage("Required", request.Culture), "PartnerId")),
        //                AllowanceDetailsResponse = null
        //            };
        //        }
        //        using (var db = new RadiusREntities())
        //        {
        //            var collections = db.PartnerCollections.Where(c => c.CollectionType == request.PartnerAllowanceRequest.AllowanceTypeId).ToArray();
        //            foreach (var item in collections)
        //            {
        //                item.PartnerRegisteredSubscriptions.FirstOrDefault().
        //            }
        //            var allowanceList = allowances.Select(a => new AllowanceDetailsResponse()
        //            {
        //                AllowanceStateID = (int)a.Key,
        //                AllowanceStateName = new LocalizedList<PartnerAllowanceState, RadiusR.Localization.Lists.PartnerAllowanceState>().GetDisplayText((int)a.Key, CreateCulture(request.Culture)),
        //                Price = a.Value
        //            }).ToArray();
        //            return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
        //            {
        //                ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
        //                AllowanceDetailsResponse = allowanceList
        //            };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Errorslogger.LogException(request.Username, ex);
        //        return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
        //        {
        //            AllowanceDetailsResponse = null,
        //            ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
        //        };
        //    }
        //}
        #region private
        private CultureInfo CreateCulture(string cultureName)
        {
            var currentCulture = CultureInfo.InvariantCulture;
            try
            {
                currentCulture = CultureInfo.CreateSpecificCulture(cultureName);
            }
            catch { }
            return currentCulture;
        }
        private string CreateErrorMessage(string LocalizationValueName, string cultureName)
        {
            return Localization.ErrorMessages.ResourceManager.GetString(LocalizationValueName, CreateCulture(cultureName));
        }        
        #endregion

    }
}
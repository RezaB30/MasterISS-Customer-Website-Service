using RadiusR.API.CustomerWebService.Requests.PartnerRequests;
using RadiusR.API.CustomerWebService.Responses.PartnerResponses;
using RadiusR.DB;
using RadiusR.DB.Utilities.Billing;
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
                    var dbPartner = db.Partners.FirstOrDefault(p => p.Email == request.PaymentRequest.UserEmail);
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
                        db.SystemLogs.Add(SystemLogProcessor.BillPayment(group.Select(bill => bill.ID), null, group.Key.ID, RadiusR.DB.Enums.SystemLogInterface.PartnerWebService, request.PaymentRequest.UserEmail, RadiusR.DB.Enums.PaymentType.Partner, gatewayName));
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
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
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
        public PartnerServiceAddSubUserResponse AddSubUser(PartnerServiceAddSubUserRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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
                    if (dbPartner.PartnerSubUsers.Any(su => su.Name.ToLower() == request.AddSubUserRequestParameters.RequestedSubUserEmail.ToLower()))
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
                            ResponseMessage = CommonResponse.NullObjectException(request.Culture)
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

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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
                    var dbSubUser = dbPartner.PartnerSubUsers.FirstOrDefault(su => su.Name == request.SubUserRequest.RequestedSubUserEmail);

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

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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
                    var dbSubUser = dbPartner.PartnerSubUsers.FirstOrDefault(su => su.Name == request.BillListRequest.SubUserEmail);

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
                        IssueDate = b.IssueDate.ToString(CultureInfo.InvariantCulture),
                        DueDate = b.DueDate.ToString(CultureInfo.InvariantCulture),
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

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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
                    var dbSubUser = dbPartner.PartnerSubUsers.FirstOrDefault(su => su.Name == request.SubUserRequest.RequestedSubUserEmail);

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

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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
                    var rawResults = dbPartner.PartnerGroup.PartnerAvailableTariffs.Select(pat => new { ID = pat.ID, TariffName = pat.Service.Name, Commitment = pat.Commitment, DomainName = pat.Domain.Name }).ToArray();
                    var list = new LocalizedList<RadiusR.DB.Enums.CommitmentLength, RadiusR.Localization.Lists.CommitmentLength>();
                    var results = rawResults.Select(rr => new KeyValueItem()
                    {
                        Key = rr.ID,
                        Value = rr.TariffName + "(" + rr.DomainName + (rr.Commitment.HasValue ? " - " + list.GetDisplayText(rr.Commitment, CreateCulture(request.Culture)) : string.Empty) + ")"
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
        public PartnerServiceKeyValueListResponse GetPartnerTariffs(PartnerServiceListFromIDRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, new ServiceSettings().Duration()))
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
    }
}
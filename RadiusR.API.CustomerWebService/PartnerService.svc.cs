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
using System.IO;
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
    [ServiceBehavior(AddressFilterMode = AddressFilterMode.Any)]
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
                    // bill register 
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
                    if (dbPartner == null)
                    {
                        return new PartnerServiceAuthenticationResponse(passwordHash, request)
                        {
                            AuthenticationResponse = new AuthenticationResponse()
                            {
                                IsAuthenticated = false
                            },
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
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
                                ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture)
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
                            PhoneNo = dbPartner.PhoneNo,
                            WorkAreas = dbPartner.WorkAreas?.Select(wa => new AuthenticationResponse.WorkAreaResult()
                            {
                                WorkAreaId = wa.ID,
                                DistrictID = wa.DistrictID,
                                DistrictName = wa.DistrictName,
                                NeighbourhoodID = wa.NeighbourhoodID,
                                NeighbourhoodName = wa.NeighbourhoodName,
                                ProvinceID = wa.ProvinceID,
                                ProvinceName = wa.ProvinceName,
                                RuralCode = wa.RuralCode
                            }).ToArray()
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
                        ServiceName = string.Empty,
                        ID = b.ID,
                        IssueDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(b.IssueDate),
                        DueDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(b.DueDate),
                        Total = b.GetPayableCost()
                    }).ToArray();
                    foreach (var item in results)
                    {
                        var billFees = db.BillFees.Where(bf => bf.BillID == item.ID).ToList();
                        if (billFees.Where(bf => bf.FeeID != null).Any())
                        {
                            var descriptions = string.Join(" - ", billFees.Select(bf => bf.Description).ToArray());
                            var fees = billFees.Where(bf => bf.Fee != null).Select(bf => bf.Fee.FeeTypeID).ToArray();
                            var feeNames = string.Join(" - ", fees.Select(f => new LocalizedList<RadiusR.DB.Enums.FeeType, RadiusR.Localization.Lists.FeeType>().GetDisplayText(f, CreateCulture(request.Culture))));
                            item.ServiceName = string.IsNullOrEmpty(descriptions) ? feeNames : $"{descriptions} - {feeNames}";
                        }
                        else
                        {
                            item.ServiceName = string.Join(" - ", billFees.Select(bf => bf.Description).ToArray());
                        }

                    }
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
                    //List<CreditChangeItem> details = new List<CreditChangeItem>();
                    if (request.CreditReportRequest.WithDetails == true)
                    {
                        var partnerCreditList = db.PartnerCredits.ToArray();
                        var getDetails = partnerCreditList.Where(pc => pc.PartnerID == dbPartner.ID).OrderByDescending(pc => pc.Date).Take(100).Select(pc => new CreditChangeItem()
                        {
                            Amount = pc.Amount,
                            Date = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(pc.Date),
                            Details = pc.Details,
                            CreditType = pc.BillID == null ? (short)Enums.PartnerCreditType.Balance : (short)Enums.PartnerCreditType.Bill
                        }).ToArray();
                        //foreach (var item in getDetails)
                        //{
                        //    var curDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(item.Date);
                        //    //RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(pc.Date)
                        //    details.Add(new CreditChangeItem()
                        //    {
                        //        Date = curDate,
                        //        Amount = item.Amount,
                        //        Details = item.Details
                        //    });
                        //}
                        return new PartnerServiceCreditReportResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            CreditReportResponse = new CreditReportResponse()
                            {
                                Total = total,
                                Details = getDetails
                            }
                        };
                    }
                    return new PartnerServiceCreditReportResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        CreditReportResponse = new CreditReportResponse()
                        {
                            Total = total,
                            Details = null
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
                    var availableTariffs = db.PartnerAvailableTariffs.Find(request.CustomerRegisterParameters.SubscriptionInfo.ServiceID);
                    var partner = db.Partners.Where(p => p.Email == request.CustomerRegisterParameters.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            NewCustomerRegisterResponse = null
                        };
                    }

                    if (availableTariffs == null)
                    {
                        return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.TariffNotFound(request.Culture),
                            NewCustomerRegisterResponse = null
                        };
                    }
                    //var externalTariff = db.ExternalTariffs.GetActiveExternalTariffs().FirstOrDefault(ext => ext.TariffID == availableTariffs.TariffID);
                    //if (externalTariff == null)
                    //{
                    //    return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                    //    {
                    //        ResponseMessage = CommonResponse.TariffNotFound(request.Culture),
                    //        NewCustomerRegisterResponse = null
                    //    };
                    //}
                    var billingPeriod = request.CustomerRegisterParameters.SubscriptionInfo.BillingPeriod; //externalTariff.Service.GetBestBillingPeriod(DateTime.Now.Day);
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
                            CustomerType = (CustomerType)request.CustomerRegisterParameters.CustomerGeneralInfo.CustomerType,
                            Email = register.CustomerGeneralInfo.Email,
                            OtherPhoneNos = register.CustomerGeneralInfo.OtherPhoneNos == null ? null : register.CustomerGeneralInfo.OtherPhoneNos.Select(p => new CustomerRegistrationInfo.PhoneNoListItem()
                            {
                                Number = p.Number
                            })
                        },
                        IDCard = register.IDCardInfo == null ? null : new CustomerRegistrationInfo.IDCardInfo()
                        {
                            BirthDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.ParseDate(register.IDCardInfo.BirthDate),
                            CardType = (IDCardTypes?)register.IDCardInfo.CardType,
                            DateOfIssue = RezaB.API.WebService.DataTypes.ServiceTypeConverter.ParseDate(register.IDCardInfo.DateOfIssue),
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
                            RegistrationType = (SubscriptionRegistrationType)register.ExtraInfo.ApplicationType,
                            TransitionPSTN = register.ExtraInfo.PSTN,
                            TransitionXDSLNo = register.ExtraInfo.XDSLNo,
                            DomainID = availableTariffs.DomainID,
                            ServiceID = availableTariffs.TariffID,
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
                            RegisteringPartner = new CustomerRegistrationInfo.RegisteringPartnerInfo()
                            {
                                PartnerID = partner.ID,
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
                    Errorslogger.LogException(request.Username, new Exception($"Birth Date : {registrationInfo.IDCard?.BirthDate}"));
                    Dictionary<string, string> valuePairs = new Dictionary<string, string>();
                    // check for existing customer
                    var dbCustomer = db.Customers.FirstOrDefault(c => c.CustomerIDCard.TCKNo == request.CustomerRegisterParameters.IDCardInfo.TCKNo && c.CustomerType == request.CustomerRegisterParameters.CustomerGeneralInfo.CustomerType);
                    Errorslogger.LogException(request.Username, new Exception($"Customer Checked"));
                    if (dbCustomer == null)
                    {
                        Errorslogger.LogException(request.Username, new Exception($"DB Customer is null. Start operation"));
                        // create new customer
                        var result = RadiusR.DB.Utilities.ComplexOperations.Subscriptions.Registration.Registration.RegisterSubscriptionWithNewCustomer(db, registrationInfo, out registeredCustomer);
                        Errorslogger.LogException(request.Username, new Exception($"After Result"));
                        if (result == null)
                        {
                            Errorslogger.LogException(request.Username, new Exception($"Result Is Null"));
                        }
                        if (!result.IsSuccess)
                        {

                            Errorslogger.LogException(request.Username, new Exception($"DB Customer is null. In Result for new register."));
                            var dic = result.ValidationMessages?.ToDictionary(x => x.Key, x => x.ToArray());
                            Errorslogger.LogException(request.Username, new Exception($"In Result for new register."));
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
                        Errorslogger.LogException(request.Username, new Exception($"RegisteredCustomer : {registeredCustomer?.Subscriptions?.FirstOrDefault()?.ID}"));
                        db.Customers.Add(registeredCustomer);
                        db.SaveChanges();
                        db.SystemLogs.Add(RadiusR.SystemLogs.SystemLogProcessor.AddSubscription(null, registeredCustomer.Subscriptions.FirstOrDefault().ID, registeredCustomer.ID, SubscriptionRegistrationType.NewRegistration, SystemLogInterface.PartnerWebService, request.Username, registeredCustomer.Subscriptions.FirstOrDefault().SubscriberNo));
                        db.SaveChanges();
                        return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                        {
                            NewCustomerRegisterResponse = valuePairs,
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        };
                    }
                    else
                    {
                        Errorslogger.LogException(request.Username, new Exception($"DB Customer is not null. Start Operation"));
                        var result = RadiusR.DB.Utilities.ComplexOperations.Subscriptions.Registration.Registration.RegisterSubscriptionForExistingCustomer(db, registrationInfo.SubscriptionInfo, dbCustomer);
                        Errorslogger.LogException(request.Username, new Exception($"After Result"));
                        if (result == null)
                        {
                            Errorslogger.LogException(request.Username, new Exception($"Result Is Null"));
                        }
                        if (!result.IsSuccess)
                        {
                            Errorslogger.LogException(request.Username, new Exception($"DB Customer is not null. In Result for existing register."));
                            var dic = result.ValidationMessages?.ToDictionary(x => x.Key, x => x.ToArray());
                            Errorslogger.LogException(request.Username, new Exception($"In Result for existing register."));
                            foreach (var item in dic)
                            {
                                if (!string.IsNullOrEmpty(item.Key))
                                {
                                    valuePairs.Add(item.Key, string.Join("-", item.Value));
                                }
                            }
                            var msg = valuePairs == null ? "dic is null" : "dic is not null";
                            Errorslogger.LogException(request.Username, new Exception($"after foreach : {msg}"));
                            return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                            {
                                NewCustomerRegisterResponse = valuePairs,
                                ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                            };
                        }
                        db.SaveChanges();
                        db.SystemLogs.Add(RadiusR.SystemLogs.SystemLogProcessor.AddSubscription(null, dbCustomer.Subscriptions.FirstOrDefault().ID, dbCustomer.ID, SubscriptionRegistrationType.NewRegistration, SystemLogInterface.PartnerWebService, request.Username, dbCustomer.Subscriptions.FirstOrDefault().SubscriberNo));
                        db.SaveChanges();
                        return new PartnerServiceNewCustomerRegisterResponse(passwordHash, request)
                        {
                            NewCustomerRegisterResponse = valuePairs,
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        };
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
        public PartnerServiceAllowanceDetailsResponse GetBasicAllowanceDetails(PartnerServiceBasicAllowanceRequest request)
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
                if (request.PartnerBasicAllowanceRequest.AllowanceTypeId == null)
                {
                    return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, string.Format(CreateErrorMessage("Required", request.Culture), "AllowanceTypeId")),
                        AllowanceDetailsResponse = null
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var partner = db.Partners.Where(p => p.Email == request.PartnerBasicAllowanceRequest.PartnerCredentials.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceAllowanceDetailsResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            AllowanceDetailsResponse = null
                        };
                    }
                    var allowances = db.GetAllowanceDetails(partner.ID, (PartnerCollectionType)request.PartnerBasicAllowanceRequest.AllowanceTypeId);
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
        public PartnerServiceSetupGenericAllowanceListResponse SetupGenericAllowanceList(PartnerServiceAllowanceRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSetupGenericAllowanceListResponse(passwordHash, request)
                    {
                        SetupGenericAllowanceList = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var partner = db.Partners.Where(p => p.Email == request.PartnerAllowanceRequest.PartnerCredentials.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceSetupGenericAllowanceListResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            SetupGenericAllowanceList = null
                        };
                    }
                    var partnerSetupList = new SetupGenericAllowanceListResponse()
                    {
                        TotalPageCount = TotalPageCount(partner.CustomerSetupUser?.CustomerSetupTasks.Count(), request.PartnerAllowanceRequest.ItemPerPage),
                        SetupGenericAllowances = partner.CustomerSetupUser?.CustomerSetupTasks.ToArray().OrderByDescending(cst => cst.TaskIssueDate).Skip((request.PartnerAllowanceRequest.PageNo.Value * request.PartnerAllowanceRequest.ItemPerPage.Value)).Take(request.PartnerAllowanceRequest.ItemPerPage.Value).Select(cst => new SetupGenericAllowanceListResponse.SetupGenericAllowanceList()
                        {
                            Allowance = cst.Allowance,
                            AllowanceState = new NameValuePair()
                            {
                                Name = GetAllowanceStateString(cst.AllowanceState, request.Culture),
                                Value = cst.AllowanceState
                            },
                            CompletionDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(cst.CompletionDate),
                            IssueDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(cst.TaskIssueDate),
                            SetupState = new NameValuePair()
                            {
                                Name = GetTaskStateString(cst.TaskStatus, request.Culture),
                                Value = cst.TaskStatus
                            },
                            SubscriptionNo = cst.Subscription.SubscriberNo
                        }).ToArray()
                    };
                    return new PartnerServiceSetupGenericAllowanceListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        SetupGenericAllowanceList = partnerSetupList
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSetupGenericAllowanceListResponse(passwordHash, request)
                {
                    SetupGenericAllowanceList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public PartnerServiceSetupAllowanceListResponse SetupAllowanceList(PartnerServiceAllowanceRequest request) // hasılat listesi
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSetupAllowanceListResponse(passwordHash, request)
                    {
                        SetupAllowanceList = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }

                using (var db = new RadiusREntities())
                {
                    var partner = db.Partners.Where(p => p.Email == request.PartnerAllowanceRequest.PartnerCredentials.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceSetupAllowanceListResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            SetupAllowanceList = null
                        };
                    }
                    var setupCollections = db.PartnerCollections.Where(pc => pc.PartnerID == partner.ID && pc.CollectionType == (short)PartnerCollectionType.Setup).ToArray();

                    var setupAllowances = setupCollections?.Select(sc => new SetupAllowanceListResponse.SetupAllowanceList()
                    {
                        ID = sc.ID,
                        IsPaid = sc.PaymentDate != null,
                        PaymentDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(sc.PaymentDate),
                        IssueDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(sc.CreationDate),
                        Total = sc.CustomerSetupTasks.Where(c => c.Allowance != null).Select(c => c.Allowance.Value).DefaultIfEmpty(0).Sum()
                    });
                    return new PartnerServiceSetupAllowanceListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        SetupAllowanceList = new SetupAllowanceListResponse()
                        {
                            TotalPageCount = TotalPageCount(setupAllowances.Count(), request.PartnerAllowanceRequest.ItemPerPage),
                            SetupAllowances = setupAllowances.ToArray()
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSetupAllowanceListResponse(passwordHash, request)
                {
                    SetupAllowanceList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public PartnerServiceSetupGenericAllowanceListResponse SetupAllowanceDetails(PartnerServiceAllowanceDetailRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSetupGenericAllowanceListResponse(passwordHash, request)
                    {
                        SetupGenericAllowanceList = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                if (request.PartnerAllowanceDetailRequest.AllowanceCollectionID == null)
                {
                    return new PartnerServiceSetupGenericAllowanceListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, string.Format(CreateErrorMessage("Required", request.Culture), "AllowanceCollectionID")),
                        SetupGenericAllowanceList = null
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var partner = db.Partners.Where(p => p.Email == request.PartnerAllowanceDetailRequest.PartnerCredentials.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceSetupGenericAllowanceListResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            SetupGenericAllowanceList = null
                        };
                    }
                    var setupCollections = db.PartnerCollections.Find(request.PartnerAllowanceDetailRequest.AllowanceCollectionID);
                    var setupAllowances = new SetupGenericAllowanceListResponse()
                    {
                        TotalPageCount = TotalPageCount(setupCollections.CustomerSetupTasks.Count(), request.PartnerAllowanceDetailRequest.ItemPerPage),
                        SetupGenericAllowances = setupCollections.CustomerSetupTasks?.ToArray().OrderByDescending(cst => cst.TaskIssueDate).Skip((request.PartnerAllowanceDetailRequest.PageNo.Value * request.PartnerAllowanceDetailRequest.ItemPerPage.Value)).Take(request.PartnerAllowanceDetailRequest.ItemPerPage.Value).Select(cst => new SetupGenericAllowanceListResponse.SetupGenericAllowanceList()
                        {
                            Allowance = cst.Allowance,
                            AllowanceState = new NameValuePair()
                            {
                                Name = GetAllowanceStateString(cst.AllowanceState, request.Culture),
                                Value = cst.AllowanceState
                            },
                            CompletionDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(cst.CompletionDate),
                            IssueDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(cst.TaskIssueDate),
                            SetupState = new NameValuePair()
                            {
                                Name = GetTaskStateString(cst.TaskStatus, request.Culture),
                                Value = cst.TaskStatus
                            },
                            SubscriptionNo = cst.Subscription.SubscriberNo
                        }).ToArray()
                    };
                    return new PartnerServiceSetupGenericAllowanceListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        SetupGenericAllowanceList = setupAllowances
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSetupGenericAllowanceListResponse(passwordHash, request)
                {
                    SetupGenericAllowanceList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public PartnerServiceSaleAllowanceListResponse SaleAllowanceList(PartnerServiceAllowanceRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSaleAllowanceListResponse(passwordHash, request)
                    {
                        SaleAllowanceList = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }

                using (var db = new RadiusREntities())
                {
                    var partner = db.Partners.Where(p => p.Email == request.PartnerAllowanceRequest.PartnerCredentials.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceSaleAllowanceListResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            SaleAllowanceList = null
                        };
                    }
                    var saleCollections = db.PartnerCollections.Where(pc => pc.PartnerID == partner.ID && pc.CollectionType == (short)RadiusR.DB.Enums.PartnerCollectionType.Sales).ToArray();

                    var saleAllowances = saleCollections?.Select(sc => new SaleAllowanceListResponse.SaleAllowanceList()
                    {
                        ID = sc.ID,
                        IsPaid = sc.PaymentDate != null,
                        PaymentDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(sc.PaymentDate),
                        IssueDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(sc.CreationDate),
                        Total = sc.CustomerSetupTasks.Where(c => c.Allowance != null).Select(c => c.Allowance.Value).DefaultIfEmpty(0).Sum()
                    });
                    return new PartnerServiceSaleAllowanceListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        SaleAllowanceList = new SaleAllowanceListResponse()
                        {
                            TotalPageCount = TotalPageCount(saleAllowances.Count(), request.PartnerAllowanceRequest.ItemPerPage),
                            SaleAllowances = saleAllowances.ToArray()
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSaleAllowanceListResponse(passwordHash, request)
                {
                    SaleAllowanceList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }

        public PartnerServiceSaleGenericAllowanceListResponse SaleAllowanceDetails(PartnerServiceAllowanceDetailRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSaleGenericAllowanceListResponse(passwordHash, request)
                    {
                        SaleGenericAllowanceList = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }

                if (request.PartnerAllowanceDetailRequest.AllowanceCollectionID == null)
                {
                    return new PartnerServiceSaleGenericAllowanceListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, string.Format(CreateErrorMessage("Required", request.Culture), "AllowanceCollectionID")),
                        SaleGenericAllowanceList = null
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var partner = db.Partners.Where(p => p.Email == request.PartnerAllowanceDetailRequest.PartnerCredentials.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceSaleGenericAllowanceListResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            SaleGenericAllowanceList = null
                        };
                    }
                    var saleCollections = db.PartnerCollections.Find(request.PartnerAllowanceDetailRequest.AllowanceCollectionID);
                    var saleAllowances = new SaleGenericAllowanceListResponse()
                    {
                        TotalPageCount = TotalPageCount(saleCollections.CustomerSetupTasks.Count(), request.PartnerAllowanceDetailRequest.ItemPerPage),
                        SaleGenericAllowances = saleCollections.PartnerRegisteredSubscriptions?.ToArray().OrderByDescending(cst => cst.Subscription.MembershipDate).Skip((request.PartnerAllowanceDetailRequest.PageNo.Value * request.PartnerAllowanceDetailRequest.ItemPerPage.Value)).Take(request.PartnerAllowanceDetailRequest.ItemPerPage.Value).Select(cst => new SaleGenericAllowanceListResponse.SaleGenericAllowanceList()
                        {
                            Allowance = cst.Allowance,
                            AllowanceState = new NameValuePair()
                            {
                                Name = GetAllowanceStateString(cst.AllowanceState, request.Culture),
                                Value = cst.AllowanceState
                            },
                            MembershipDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(cst.Subscription.MembershipDate),
                            SaleState = new NameValuePair()
                            {
                                Name = GetSubscriptionStateString(cst.Subscription.State, request.Culture),
                                Value = cst.Subscription.State
                            },
                            SubscriptionNo = cst.Subscription.SubscriberNo
                        }).ToArray()
                    };
                    return new PartnerServiceSaleGenericAllowanceListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        SaleGenericAllowanceList = saleAllowances
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSaleGenericAllowanceListResponse(passwordHash, request)
                {
                    SaleGenericAllowanceList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public PartnerServiceSaleGenericAllowanceListResponse SaleGenericAllowanceList(PartnerServiceAllowanceRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSaleGenericAllowanceListResponse(passwordHash, request)
                    {
                        SaleGenericAllowanceList = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }

                using (var db = new RadiusREntities())
                {
                    var partner = db.Partners.Where(p => p.Email == request.PartnerAllowanceRequest.PartnerCredentials.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceSaleGenericAllowanceListResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            SaleGenericAllowanceList = null
                        };
                    }
                    var partnerSetupList = new SaleGenericAllowanceListResponse()
                    {
                        TotalPageCount = TotalPageCount(partner.PartnerRegisteredSubscriptions?.Count(), request.PartnerAllowanceRequest.ItemPerPage),
                        SaleGenericAllowances = partner.PartnerRegisteredSubscriptions?.ToArray().OrderByDescending(prs => prs.Subscription.MembershipDate).Skip((request.PartnerAllowanceRequest.PageNo.Value * request.PartnerAllowanceRequest.ItemPerPage.Value)).Take(request.PartnerAllowanceRequest.ItemPerPage.Value).Select(prs => new SaleGenericAllowanceListResponse.SaleGenericAllowanceList()
                        {
                            Allowance = prs.Allowance,
                            AllowanceState = new NameValuePair()
                            {
                                Name = GetAllowanceStateString(prs.AllowanceState, request.Culture),
                                Value = prs.AllowanceState
                            },
                            MembershipDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(prs.Subscription.MembershipDate),
                            SaleState = new NameValuePair()
                            {
                                Name = GetSubscriptionStateString(prs.Subscription.State, request.Culture),
                                Value = prs.Subscription.State
                            },
                            SubscriptionNo = prs.Subscription.SubscriberNo
                        }).ToArray()
                    };
                    return new PartnerServiceSaleGenericAllowanceListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        SaleGenericAllowanceList = partnerSetupList
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSaleGenericAllowanceListResponse(passwordHash, request)
                {
                    SaleGenericAllowanceList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public PartnerServiceSubscriptionsResponse GetPartnerSubscriptions(PartnerServiceSubscriptionsRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSubscriptionsResponse(passwordHash, request)
                    {
                        PartnerSubscriptionList = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var partner = db.Partners.Where(p => p.Email == request.SubscriptionsRequestParameters.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceSubscriptionsResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            PartnerSubscriptionList = null
                        };
                    }
                    var partnerSubscriptions = partner.PartnerRegisteredSubscriptions.ToList();
                    return new PartnerServiceSubscriptionsResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        PartnerSubscriptionList = partnerSubscriptions?.Select(ps => new PartnerSubscriptionsResponse()
                        {
                            ID = ps.SubscriptionID,
                            CustomerState = new NameValuePair()
                            {
                                Value = ps.Subscription.State,
                                Name = new LocalizedList<CustomerState, RadiusR.Localization.Lists.CustomerState>().GetDisplayText(ps.Subscription.State, CreateCulture(request.Culture))
                            },
                            DisplayName = ps.Subscription.ValidDisplayName,
                            MembershipDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateString(ps.Subscription.MembershipDate),
                            SubscriberNo = ps.Subscription.SubscriberNo
                        }).ToArray()
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSubscriptionsResponse(passwordHash, request)
                {
                    PartnerSubscriptionList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public PartnerServiceClientAttachmentsResponse GetPartnerClientAttachments(PartnerServiceClientAttachmentsRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceClientAttachmentsResponse(passwordHash, request)
                    {
                        ClientAttachmentList = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var partner = db.Partners.Where(p => p.Email == request.ClientAttachmentsParameters.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceClientAttachmentsResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            ClientAttachmentList = null
                        };
                    }
                    if (request.ClientAttachmentsParameters.SubscriptionId == null)
                    {
                        return new PartnerServiceClientAttachmentsResponse(passwordHash, request)
                        {
                            ClientAttachmentList = null,
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture)
                        };
                    }
                    var fileManager = new RadiusR.FileManagement.MasterISSFileManager();
                    var attachmentList = fileManager.GetClientAttachmentsList(request.ClientAttachmentsParameters.SubscriptionId.Value).Result?.ToList();
                    var attachmentResultList = new List<PartnerClientAttachmentsResponse>();
                    foreach (var item in attachmentList)
                    {
                        var getAttachment = fileManager.GetClientAttachment(request.ClientAttachmentsParameters.SubscriptionId.Value, item.ServerSideName);
                        if (getAttachment.Result != null)
                        {
                            byte[] content = null;
                            using (var memoryStream = new MemoryStream())
                            {
                                getAttachment.Result.Content.CopyTo(memoryStream);
                                content = memoryStream.ToArray();
                            }
                            attachmentResultList.Add(new PartnerClientAttachmentsResponse()
                            {
                                FileContent = content,
                                MIMEType = getAttachment.Result.FileDetail.MIMEType,
                                AttachmentType = (int)item.AttachmentType,
                                FileName = getAttachment.Result.FileDetail.ServerSideName
                            });
                        }
                    }
                    return new PartnerServiceClientAttachmentsResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        ClientAttachmentList = attachmentResultList.ToArray()
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceClientAttachmentsResponse(passwordHash, request)
                {
                    ClientAttachmentList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public PartnerServiceClientFormsResponse GetPartnerClientForms(PartnerServiceClientFormsRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceClientFormsResponse(passwordHash, request)
                    {
                        PartnerClientForms = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var partner = db.Partners.Where(p => p.Email == request.ClientFormsParameters.UserEmail).FirstOrDefault();
                    if (partner == null)
                    {
                        return new PartnerServiceClientFormsResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            PartnerClientForms = null
                        };
                    }
                    if (request.ClientFormsParameters.SubscriptionId == null)
                    {
                        return new PartnerServiceClientFormsResponse(passwordHash, request)
                        {
                            PartnerClientForms = null,
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture)
                        };
                    }
                    var formType = GeneralPDFFormTypes.ContractForm;
                    var subscriptionId = request.ClientFormsParameters.SubscriptionId.Value;
                    if (request.ClientFormsParameters.FormType.HasValue)
                    {
                        formType = (GeneralPDFFormTypes)request.ClientFormsParameters.FormType.Value;
                    }
                    switch (formType)
                    {
                        case GeneralPDFFormTypes.ContractForm:
                            {
                                var createdPDF = RadiusR.PDFForms.PDFWriter.GetContractPDF(db, subscriptionId);
                                byte[] content = null;
                                using (var memoryStream = new MemoryStream())
                                {
                                    createdPDF.Result.CopyTo(memoryStream);
                                    content = memoryStream.ToArray();
                                }
                                return new PartnerServiceClientFormsResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                    PartnerClientForms = new PartnerClientFormsResponse()
                                    {
                                        FileContent = content,
                                        FileName = new LocalizedList<GeneralPDFFormTypes, RadiusR.Localization.Lists.GeneralPDFFormTypes>()
                                        .GetDisplayText((int)GeneralPDFFormTypes.ContractForm, CreateCulture(request.Culture)),
                                        FormType = (int)GeneralPDFFormTypes.ContractForm,
                                        MIMEType = "application/pdf"
                                    }
                                };
                            }
                        case GeneralPDFFormTypes.TransitionForm:
                            {
                                var createdPDF = RadiusR.PDFForms.PDFWriter.GetTransitionPDF(db, subscriptionId);
                                byte[] content = null;
                                using (var memoryStream = new MemoryStream())
                                {
                                    createdPDF.Result.CopyTo(memoryStream);
                                    content = memoryStream.ToArray();
                                }
                                return new PartnerServiceClientFormsResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                    PartnerClientForms = new PartnerClientFormsResponse()
                                    {
                                        FileContent = content,
                                        FileName = new LocalizedList<GeneralPDFFormTypes, RadiusR.Localization.Lists.GeneralPDFFormTypes>()
                                        .GetDisplayText((int)GeneralPDFFormTypes.TransitionForm, CreateCulture(request.Culture)),
                                        FormType = (int)GeneralPDFFormTypes.TransitionForm,
                                        MIMEType = "application/pdf"
                                    }
                                };
                            }
                        case GeneralPDFFormTypes.PSTNtoNakedForm:
                            {
                                var createdPDF = RadiusR.PDFForms.PDFWriter.GetPSTNtoNakedPDF(db, subscriptionId);
                                byte[] content = null;
                                using (var memoryStream = new MemoryStream())
                                {
                                    createdPDF.Result.CopyTo(memoryStream);
                                    content = memoryStream.ToArray();
                                }
                                return new PartnerServiceClientFormsResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                    PartnerClientForms = new PartnerClientFormsResponse()
                                    {
                                        FileContent = content,
                                        FileName = new LocalizedList<GeneralPDFFormTypes, RadiusR.Localization.Lists.GeneralPDFFormTypes>()
                                        .GetDisplayText((int)GeneralPDFFormTypes.PSTNtoNakedForm, CreateCulture(request.Culture)),
                                        FormType = (int)GeneralPDFFormTypes.PSTNtoNakedForm,
                                        MIMEType = "application/pdf"
                                    }
                                };
                            }
                        default:
                            {
                                return new PartnerServiceClientFormsResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                    PartnerClientForms = null
                                };
                            }
                    }
                }

            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceClientFormsResponse(passwordHash, request)
                {
                    PartnerClientForms = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public PartnerServiceSaveClientAttachmentResponse SaveClientAttachment(PartnerServiceSaveClientAttachmentRequest request)
        {
            var password = new ServiceSettings().GetPartnerUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new PartnerServiceSaveClientAttachmentResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                        SaveClientAttachmentResult = false
                    };
                }
                if (request.SaveClientAttachmentParameters == null || request.SaveClientAttachmentParameters.SubscriptionId == null)
                {
                    return new PartnerServiceSaveClientAttachmentResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture),
                        SaveClientAttachmentResult = false
                    };
                }
                var fileManager = new RadiusR.FileManagement.MasterISSFileManager();
                var fileStream = new MemoryStream(request.SaveClientAttachmentParameters.FileContent);
                var saveAttachment = fileManager.SaveClientAttachment(
                    request.SaveClientAttachmentParameters.SubscriptionId.Value,
                    new FileManagement.SpecialFiles.FileManagerClientAttachmentWithContent(fileStream,
                    new FileManagement.SpecialFiles.FileManagerClientAttachment((FileManagement.SpecialFiles.ClientAttachmentTypes)request.SaveClientAttachmentParameters.AttachmentType,
                    request.SaveClientAttachmentParameters.FileExtention)));
                if (saveAttachment.InternalException != null)
                {
                    return new PartnerServiceSaveClientAttachmentResponse(passwordHash, request)
                    {
                        SaveClientAttachmentResult = false,
                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, saveAttachment.InternalException.Message)
                    };
                }
                return new PartnerServiceSaveClientAttachmentResponse(passwordHash, request)
                {
                    SaveClientAttachmentResult = saveAttachment.Result,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new PartnerServiceSaveClientAttachmentResponse(passwordHash, request)
                {
                    SaveClientAttachmentResult = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        #region private
        private int TotalPageCount(int? TotalRow, int? itemPerPage)
        {
            itemPerPage = itemPerPage ?? 10;
            var count = !TotalRow.HasValue ? 0 : (TotalRow % itemPerPage) == 0 ?
                        (TotalRow / itemPerPage) :
                        (TotalRow / itemPerPage) + 1;
            return count ?? 0;
        }
        private string GetAllowanceStateString(int stateId, string culture)
        {
            var stateText = new LocalizedList<PartnerAllowanceState, RadiusR.Localization.Lists.PartnerAllowanceState>().GetDisplayText(stateId, CreateCulture(culture));
            return stateText;
        }
        private string GetTaskStateString(int stateId, string culture)
        {
            var stateText = new LocalizedList<RadiusR.DB.Enums.CustomerSetup.TaskStatuses, RadiusR.Localization.Lists.CustomerSetup.TaskStatuses>().GetDisplayText(stateId, CreateCulture(culture));
            return stateText;
        }
        private string GetSubscriptionStateString(int stateId, string culture)
        {
            var stateText = new LocalizedList<RadiusR.DB.Enums.CustomerState, RadiusR.Localization.Lists.CustomerState>().GetDisplayText(stateId, CreateCulture(culture));
            return stateText;
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
        private string CreateErrorMessage(string LocalizationValueName, string cultureName)
        {
            return Localization.ErrorMessages.ResourceManager.GetString(LocalizationValueName, CreateCulture(cultureName));
        }
        #endregion

    }
}
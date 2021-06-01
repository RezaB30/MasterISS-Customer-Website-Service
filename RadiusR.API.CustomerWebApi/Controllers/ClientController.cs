using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using RadiusR.API.CustomerWebService.Requests;
using RadiusR.API.CustomerWebService.Responses;
using RadiusR.API.CustomerWebService.Enums;
using RadiusR.API.CustomerWebService.Responses.Payment;
using RadiusR.API.CustomerWebService.Requests.Payment;
using RadiusR.API.CustomerWebService;
using System.Security.Cryptography;
using RezaB.API.WebService.NLogExtentions;
using RezaB.API.WebService;
using RadiusR.DB;
using RadiusR.API.CustomerWebService.Responses.Support;
using NLog;
using RadiusR.API.CustomerWebService.Requests.Support;
using RadiusR.API.MobilExpress.DBAdapter.AdapterClient;
using RadiusR.DB.Enums;
using RadiusR.DB.Enums.RecurringDiscount;
using RadiusR.DB.ModelExtentions;
using RadiusR.DB.Settings;
using RadiusR.DB.Utilities.Billing;
using RadiusR.DB.Utilities.ComplexOperations.Subscriptions.Registration;
using RadiusR.SMS;
using RadiusR.SystemLogs;
using RadiusR.VPOS;
using RezaB.Data.Localization;
using RezaB.TurkTelekom.WebServices;
using RezaB.TurkTelekom.WebServices.Address;
using RezaB.TurkTelekom.WebServices.Availability;
using RezaB.TurkTelekom.WebServices.TTOYS;
using System.Data.Entity.SqlServer;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;

namespace RadiusR.API.CustomerWebApi.Controllers
{
    public class ClientController : ApiController
    {
        Logger ErrorsLogger = LogManager.GetLogger("Errors");
        Logger CustomerInComingInfo = LogManager.GetLogger("CustomerInComingInfo");
        Logger MobileLogger = LogManager.GetLogger("MobileAppLog");
        readonly RadiusR.Address.AddressManager AddressClient = new RadiusR.Address.AddressManager();
        [HttpPost]
        public CustomerServiceActivateAutomaticPaymentResponse ActivateAutomaticPayment(CustomerServiceActivateAutomaticPaymentRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceActivateAutomaticPaymentResponse(passwordHash, request)
                    {
                        IsSuccess = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (!MobilExpressSettings.MobilExpressIsActive)
                {
                    return new CustomerServiceActivateAutomaticPaymentResponse(passwordHash, request)
                    {
                        IsSuccess = false,
                        ResponseMessage = CommonResponse.MobilexpressIsDeactive(request.Culture),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbSubscription = db.Subscriptions.Find(request.ActivateAutomaticPaymentParameters.SubscriptionId);
                    var currentCustomer = dbSubscription.Customer;
                    //if (dbSubscription.CustomerID != currentCustomer.ID || dbSubscription.IsCancelled)
                    //{
                    //    Errorslogger.Warn($"Customer is not found or Customer is cancelled");
                    //    return new CustomerServiceActivateAutomaticPaymentResponse(passwordHash, request)
                    //    {
                    //        
                    //        Data = false,
                    //        
                    //        ResponseMessage = CommonResponse.InvalidOperation(request.Culture)
                    //    };
                    //    //return RedirectToAction("AutomaticPayment"); // invalid operation
                    //}

                    var client = new MobilExpressAdapterClient(MobilExpressSettings.MobilExpressMerchantKey, MobilExpressSettings.MobilExpressAPIPassword, new ClientConnectionDetails()
                    {
                        IP = request.ActivateAutomaticPaymentParameters.HttpContextParameters.UserHostAddress,
                        UserAgent = request.ActivateAutomaticPaymentParameters.HttpContextParameters.UserAgent
                    });

                    var response = client.GetCards(currentCustomer);
                    if (response.InternalException != null)
                    {
                        ErrorsLogger.Error(response.InternalException);
                        //Errorslogger.Warn(response.InternalException, "Error calling 'GetCards' from MobilExpress client");
                        return new CustomerServiceActivateAutomaticPaymentResponse(passwordHash, request)
                        {
                            IsSuccess = false,
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.Common.GeneralError)
                        };
                    }
                    if (response.Response.ResponseCode != RezaB.API.MobilExpress.Response.ResponseCodes.Success)
                    {
                        ErrorsLogger.Error(new Exception($"Error calling 'GetCards' from MobilExpress client . Message : {response.Response.ErrorMessage}{Environment.NewLine} Subscription Id : {request.ActivateAutomaticPaymentParameters.SubscriptionId}{Environment.NewLine} Card Token : {request.ActivateAutomaticPaymentParameters.CardToken}"));
                        //Errorslogger.Warn($"Error calling 'GetCards' from MobilExpress client . Message : {response.Response.ErrorMessage}{Environment.NewLine} Subscription Id : {request.ActivateAutomaticPaymentParameters.SubscriptionId}{Environment.NewLine} Card Token : {request.ActivateAutomaticPaymentParameters.CardToken}");
                        return new CustomerServiceActivateAutomaticPaymentResponse(passwordHash, request)
                        {
                            IsSuccess = false,
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, response.Response.ErrorMessage)
                        };
                    }
                    var currentCard = response.Response.CardList.FirstOrDefault(c => c.CardToken == request.ActivateAutomaticPaymentParameters.CardToken);
                    if (currentCard == null)
                    {
                        return new CustomerServiceActivateAutomaticPaymentResponse(passwordHash, request)
                        {
                            IsSuccess = false,
                            ResponseMessage = CommonResponse.CardInformationNotFound(request.Culture)
                        };
                    }
                    dbSubscription.MobilExpressAutoPayment = new MobilExpressAutoPayment()
                    {
                        CardToken = request.ActivateAutomaticPaymentParameters.CardToken,
                        PaymentType = request.ActivateAutomaticPaymentParameters.PaymentType.Value
                    };
                    db.SaveChanges();
                    var smsClient = new SMSService();
                    db.SMSArchives.AddSafely(smsClient.SendSubscriberSMS(dbSubscription, SMSType.MobilExpressActivation, new Dictionary<string, object> {
                        { SMSParamaterRepository.SMSParameterNameCollection.CardNo, currentCard.MaskedCardNumber }
                    }));
                    db.SystemLogs.Add(SystemLogProcessor.ActivateAutomaticPayment(dbSubscription.ID, SystemLogInterface.CustomerWebsite, request.Username, "MobilExpress"));
                    db.SaveChanges();
                    return new CustomerServiceActivateAutomaticPaymentResponse(passwordHash, request)
                    {
                        IsSuccess = true,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceActivateAutomaticPaymentResponse(passwordHash, request)
                {
                    IsSuccess = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        [HttpPost]
        public CustomerServiceAddCardResponse AddCard(CustomerServiceAddCardRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceAddCardResponse(passwordHash, request)
                    {
                        IsSuccess = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (!MobilExpressSettings.MobilExpressIsActive)
                {
                    return new CustomerServiceAddCardResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.MobilexpressIsDeactive(request.Culture),
                        IsSuccess = null
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    //var smsCode = CacheManager.GetKey(request.Username, request.SubscriptionParameters.SubscriptionId.ToString(), CacheTypes.AddCardSMSValidation);
                    //if (request.Data.SMSCode != smsCode)
                    //{
                    //    return new CustomerServiceAddCardResponse(passwordHash, request)
                    //    {
                    //        
                    //        Data = false,
                    //        
                    //        ResponseMessage = CommonResponse.WrongSMSValidation(request.Culture)
                    //    };
                    //}
                    var dbClient = db.Subscriptions.Find(request.AddCardParameters.SubscriptionId);
                    var dbCustomer = db.Subscriptions.Find(request.AddCardParameters.SubscriptionId).Customer;
                    var client = new MobilExpress.DBAdapter.AdapterClient.MobilExpressAdapterClient(MobilExpressSettings.MobilExpressMerchantKey, MobilExpressSettings.MobilExpressAPIPassword, new MobilExpress.DBAdapter.AdapterClient.ClientConnectionDetails()
                    {
                        IP = request.AddCardParameters.HttpContextParameters.UserHostAddress,
                        UserAgent = request.AddCardParameters.HttpContextParameters.UserAgent
                    });
                    var response = client.SaveCard(dbCustomer, new RadiusR.API.MobilExpress.DBAdapter.AdapterParameters.AdapterCard()
                    {
                        CardHolderName = request.AddCardParameters.CardholderName,
                        CardNumber = request.AddCardParameters.CardNo.Replace("-", ""),
                        CardMonth = Convert.ToInt32(request.AddCardParameters.ExpirationMonth),
                        CardYear = Convert.ToInt32("20" + request.AddCardParameters.ExpirationYear)
                    });

                    if (response.InternalException != null)
                    {
                        ErrorsLogger.Error(response.InternalException);
                        return new CustomerServiceAddCardResponse(passwordHash, request)
                        {
                            IsSuccess = false,
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.Common.GeneralError)
                        };
                    }
                    if (response.Response.ResponseCode != RezaB.API.MobilExpress.Response.ResponseCodes.Success)
                    {
                        ErrorsLogger.Error(new Exception(response.Response.ErrorMessage + "-" + response.Response.ResponseCode.ToString()));
                        return new CustomerServiceAddCardResponse(passwordHash, request)
                        {
                            IsSuccess = false,
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, response.Response.ErrorMessage)
                        };
                    }

                    var cardNo = request.AddCardParameters.CardNo.Replace("-", "");
                    db.SystemLogs.Add(SystemLogProcessor.AddCreditCard(dbCustomer.ID, SystemLogInterface.CustomerWebsite, request.Username, cardNo.Substring(0, 6) + "******" + cardNo.Substring(12)));
                    db.SaveChanges();
                    return new CustomerServiceAddCardResponse(passwordHash, request)
                    {
                        IsSuccess = true,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceAddCardResponse(passwordHash, request)
                {

                    IsSuccess = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public CustomerServiceAddCardSMSValidationResponse AddCardSMSCheck(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceAddCardSMSValidationResponse(passwordHash, request)
                    {
                        SMSCode = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (!MobilExpressSettings.MobilExpressIsActive)
                {
                    return new CustomerServiceAddCardSMSValidationResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.MobilexpressIsDeactive(request.Culture),
                        SMSCode = null
                    };
                }
                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbSubscription = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId);
                    var rand = new Random();
                    var smsCode = rand.Next(100000, 1000000).ToString();
                    var smsClient = new SMSService();
                    var smsResponse = smsClient.SendSubscriberSMS(dbSubscription, SMSType.MobilExpressAddRemoveCard, new Dictionary<string, object>() {
                        { SMSParamaterRepository.SMSParameterNameCollection.SMSCode, smsCode }
                    });
                    ErrorsLogger.Info($"SubscriptionID : {dbSubscription.ID} - AddCardSMSCode : {smsCode} - SMSText : {smsResponse?.Text} - SMSType : {smsResponse?.SMSTypeID}");
                    //CacheManager.GenerateKey(request.Username, request.SubscriptionParameters.SubscriptionId.ToString(), smsCode, CacheTypes.AddCardSMSValidation, new TimeSpan(0, 15, 0));
                    return new CustomerServiceAddCardSMSValidationResponse(passwordHash, request)
                    {
                        SMSCode = smsCode,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceAddCardSMSValidationResponse(passwordHash, request)
                {

                    SMSCode = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public CustomerServiceAuthenticationSMSConfirmResponse AuthenticationSMSConfirm(CustomerServiceAuthenticationSMSConfirmRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceAuthenticationSMSConfirmResponse(passwordHash, request)
                    {

                        AuthenticationSMSConfirmResponse = null,

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                using (var db = new RadiusREntities())
                {
                    // find customers
                    var dbCustomers = db.Customers.Where(c => c.CustomerIDCard.TCKNo == request.AuthenticationSMSConfirmParameters.CustomerCode || c.ContactPhoneNo == request.AuthenticationSMSConfirmParameters.CustomerCode).ToArray();
                    // select a subscriber
                    var dbClient = dbCustomers.SelectMany(c => c.Subscriptions).FirstOrDefault();
                    var selectedDbClient = dbCustomers.SelectMany(c => c.Subscriptions).Where(s => s.State == (short)CustomerState.Active).FirstOrDefault() ?? dbClient;
                    if (dbCustomers.Count() > 0 && dbClient != null)
                    {

                        // if need to send a new password
                        if (string.IsNullOrEmpty(dbClient.OnlinePassword) || !dbClient.OnlinePasswordExpirationDate.HasValue)
                            return new CustomerServiceAuthenticationSMSConfirmResponse(passwordHash, request)
                            {
                                AuthenticationSMSConfirmResponse = null,
                                ResponseMessage = CommonResponse.FailedResponse(request.Culture)
                            };
                        if (dbClient.OnlinePassword != request.AuthenticationSMSConfirmParameters.SMSPassword)
                        {
                            return new CustomerServiceAuthenticationSMSConfirmResponse(passwordHash, request)
                            {
                                AuthenticationSMSConfirmResponse = null,
                                ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.Common.ResourceManager.GetString("SMSPasswordWrong", CultureInfo.CreateSpecificCulture(request.Culture))),

                            };
                        }
                        // sign in
                        var relatedCustomers = GetRelatedCustomers(dbCustomers); //dbCustomers.SelectMany(c => c.Subscriptions).Select(s => s.ID + "," + s.SubscriberNo).ToArray();
                        return new CustomerServiceAuthenticationSMSConfirmResponse(password, request)
                        {
                            AuthenticationSMSConfirmResponse = new AuthenticationSMSConfirmResponse()
                            {
                                ID = selectedDbClient.ID,
                                SubscriberNo = selectedDbClient.SubscriberNo,
                                ValidDisplayName = selectedDbClient.ValidDisplayName,
                                RelatedCustomers = relatedCustomers
                            },


                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        };
                    }
                    return new CustomerServiceAuthenticationSMSConfirmResponse(passwordHash, request)
                    {

                        AuthenticationSMSConfirmResponse = null,
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),

                    };
                }
            }
            catch (Exception ex)
            {
                return new CustomerServiceAuthenticationSMSConfirmResponse(passwordHash, request)
                {


                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                    AuthenticationSMSConfirmResponse = null
                };
            }
        }

        [HttpPost]
        public CustomerServiceBillPayableAmountResponse BillPayableAmount(CustomerServiceBillPayableAmountRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    //Errorslogger.Error($"unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceBillPayableAmountResponse(passwordHash, request)
                    {


                        PayableAmount = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.BillPayableAmountParameters.SubscriptionId == null)
                {
                    ErrorsLogger.Error(new Exception("Subscription is null"));
                    //Errorslogger.Debug($"Subscription is null.");
                    return new CustomerServiceBillPayableAmountResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        PayableAmount = null,
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbSubscription = db.Subscriptions.Find(request.BillPayableAmountParameters.SubscriptionId);
                    // pre-paid sub
                    if (!dbSubscription.HasBilling)
                    {
                        //Errorslogger.Debug($"HasBilling calling false. Subscription Id : {request.BillPayableAmountParameters.SubscriptionId}");
                        ErrorsLogger.Error(new Exception($"HasBilling calling false. Subscription Id : {request.BillPayableAmountParameters.SubscriptionId}"));
                        return new CustomerServiceBillPayableAmountResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            PayableAmount = dbSubscription.GetSubscriberPackageExtentionUnitPrice(),
                        };
                    }
                    // billed sub
                    var creditsAmount = dbSubscription.SubscriptionCredits.Sum(credit => credit.Amount);
                    var bills = dbSubscription.Bills.Where(bill => bill.BillStatusID == (short)BillState.Unpaid).OrderBy(bill => bill.IssueDate).AsEnumerable();
                    if (request.BillPayableAmountParameters.BillId.HasValue)
                    {
                        bills = bills.Where(bill => bill.ID == request.BillPayableAmountParameters.BillId.Value);
                    }
                    if (!bills.Any())
                    {
                        return new CustomerServiceBillPayableAmountResponse(passwordHash, request)
                        {

                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            PayableAmount = 0m,

                        };
                    }
                    var billsAmount = bills.Sum(bill => bill.GetPayableCost());
                    if (!dbSubscription.HasBilling)
                    {
                        billsAmount = dbSubscription.Service.Price;
                    }
                    return new CustomerServiceBillPayableAmountResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        PayableAmount = Math.Max(0m, billsAmount - creditsAmount),

                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceBillPayableAmountResponse(passwordHash, request)
                {


                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    PayableAmount = null,
                };
            }
        }

        [HttpPost]
        public CustomerServiceCanHaveQuotaSaleResponse CanHaveQuotaSale(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    //Errorslogger.Error($"CanHaveQuotaSale unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceCanHaveQuotaSaleResponse(passwordHash, request)
                    {


                        CanHaveQuotaSale = false,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    var dbClient = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId);
                    if (dbClient == null)
                    {
                        return new CustomerServiceCanHaveQuotaSaleResponse(passwordHash, request)
                        {

                            CanHaveQuotaSale = false,

                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture)
                        };
                    }
                    if (dbClient.Service.CanHaveQuotaSale)
                    {
                        return new CustomerServiceCanHaveQuotaSaleResponse(passwordHash, request)
                        {
                            CanHaveQuotaSale = true,

                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                        };
                    }
                    return new CustomerServiceCanHaveQuotaSaleResponse(passwordHash, request)
                    {


                        CanHaveQuotaSale = false,
                        ResponseMessage = CommonResponse.QuotaTariffNotFound(request.Culture),
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceCanHaveQuotaSaleResponse(passwordHash, request)
                {


                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    CanHaveQuotaSale = false,
                };
            }
        }

        [HttpPost]
        public CustomerServiceChangeSubClientResponse ChangeSubClient(CustomerServiceChangeSubClientRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    //Errorslogger.Error($" unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceChangeSubClientResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),

                        ChangeSubClientResponse = null
                    };
                }
                if (request.ChangeSubClientRequest.CurrentSubscriptionID == null || request.ChangeSubClientRequest.TargetSubscriptionID == null)
                {
                    return new CustomerServiceChangeSubClientResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),

                        ChangeSubClientResponse = null
                    };
                }
                using (var db = new RadiusREntities())
                {
                    // current subscription
                    var currentClient = db.Subscriptions.Find(request.ChangeSubClientRequest.CurrentSubscriptionID);
                    // target subscription
                    var targetClient = db.Subscriptions.Find(request.ChangeSubClientRequest.TargetSubscriptionID);
                    if (currentClient.Customer.CustomerIDCard.TCKNo != targetClient.Customer.CustomerIDCard.TCKNo && currentClient.Customer.ContactPhoneNo != targetClient.Customer.ContactPhoneNo)
                    {
                        return new CustomerServiceChangeSubClientResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                            ChangeSubClientResponse = null
                        };
                    }
                    if (!targetClient.Bills.Where(b => b.BillStatusID == (short)BillState.Unpaid).Any() && targetClient.State == (short)CustomerState.Cancelled)
                    {
                        return new CustomerServiceChangeSubClientResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                            ChangeSubClientResponse = null
                        };
                    }
                    if (targetClient.State == (short)CustomerState.Dismissed)
                    {
                        return new CustomerServiceChangeSubClientResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                            ChangeSubClientResponse = null
                        };
                    }
                    // find customers
                    var dbCustomers = db.Customers.Where(c => c.CustomerIDCard.TCKNo == targetClient.Customer.CustomerIDCard.TCKNo || c.ContactPhoneNo == targetClient.Customer.ContactPhoneNo).ToArray();
                    var relatedCustomers = GetRelatedCustomers(dbCustomers); //dbCustomers.SelectMany(c => c.Subscriptions).Select(s => s.ID + "," + s.SubscriberNo).ToArray();
                    return new CustomerServiceChangeSubClientResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                        ChangeSubClientResponse = new ChangeSubClientResponse()
                        {
                            ID = targetClient.ID,
                            RelatedCustomers = relatedCustomers,
                            ValidDisplayName = targetClient.ValidDisplayName,
                            SubscriberNo = targetClient.SubscriberNo
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceChangeSubClientResponse(passwordHash, request)
                {


                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    ChangeSubClientResponse = null,
                };
            }
        }

        [HttpPost]
        public CustomerServiceConnectionStatusResponse ConnectionStatus(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                    {
                        //Errorslogger.Error($"ConnectionStatus unauthorize error. User : {request.Username}");
                        ErrorsLogger.Error(new Exception("unauthorize error"));
                        return new CustomerServiceConnectionStatusResponse(passwordHash, request)
                        {

                            ResponseMessage = CommonResponse.UnauthorizedResponse(request),

                            GetCustomerConnectionStatusResponse = null
                        };
                    }
                    var Subscription = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId);
                    if (Subscription == null)
                    {
                        //Errorslogger.Error($"ConnectionStatus -> subscription is not found . User : {request.Username}");
                        ErrorsLogger.Error(new Exception($"ConnectionStatus -> subscription is not found"));
                        return new CustomerServiceConnectionStatusResponse(passwordHash, request)
                        {
                            GetCustomerConnectionStatusResponse = null,
                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture)
                        };
                    }

                    var domainCache = RadiusR.DB.DomainsCache.DomainsCache.GetDomainByID(Subscription.DomainID);
                    if (domainCache.TelekomCredential == null)
                    {
                        return new CustomerServiceConnectionStatusResponse(passwordHash, request)
                        {

                            GetCustomerConnectionStatusResponse = null,
                            ResponseMessage = CommonResponse.TelekomCredentialNotFound(request.Culture),

                        };
                    }
                    RezaB.TurkTelekom.WebServices.TTOYS.TTOYSServiceClient client = new RezaB.TurkTelekom.WebServices.TTOYS.TTOYSServiceClient(domainCache.TelekomCredential.XDSLWebServiceUsernameInt, domainCache.TelekomCredential.XDSLWebServicePassword);
                    var Result = client.Check(Subscription.SubscriptionTelekomInfo.SubscriptionNo);
                    if (Result.InternalException != null)
                    {
                        return new CustomerServiceConnectionStatusResponse(passwordHash, request)
                        {


                            GetCustomerConnectionStatusResponse = null,
                            ResponseMessage = CommonResponse.TelekomWebServiceError(request.Culture, Result.InternalException.Message)
                        };
                        //TTErrorslogger.Error(Result.InternalException, "Error telekom line state");
                    }
                    var xdslTypeText = new LocalizedList<XDSLType, RadiusR.Localization.Lists.XDSLType>().GetDisplayText(Subscription.SubscriptionTelekomInfo.XDSLType, CultureInfo.CreateSpecificCulture(request.Culture));
                    var connectionStatusText = new LocalizedList<TTOYSServiceClient.OperationStatus, RadiusR.Localization.Lists.TTLineState>().GetDisplayText((short)Result.Data.OperationStatus, CultureInfo.CreateSpecificCulture(request.Culture));
                    return new CustomerServiceConnectionStatusResponse(passwordHash, request)
                    {

                        GetCustomerConnectionStatusResponse = new GetCustomerConnectionStatusResponse()
                        {
                            ConnectionStatusValue = (short)Result.Data.OperationStatus,
                            ConnectionStatusText = connectionStatusText,
                            CurrentDownload = Result.Data.CurrentDown,
                            CurrentUpload = Result.Data.CurrentUp,
                            XDSLNo = Subscription.SubscriptionTelekomInfo.SubscriptionNo,
                            XDSLTypeValue = Subscription.SubscriptionTelekomInfo.XDSLType.Value,
                            XDSLTypeText = xdslTypeText,
                            DownloadMargin = Result.Data.NoiseRateDown,
                            UploadMargin = Result.Data.NoiseRateUp
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                    };
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceConnectionStatusResponse(passwordHash, request)
                {

                    ResponseMessage = CommonResponse.NullObjectException(request.Culture),

                    GetCustomerConnectionStatusResponse = null
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceConnectionStatusResponse(passwordHash, request)
                {

                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                    GetCustomerConnectionStatusResponse = null
                };
            }
        }

        [HttpPost]
        public CustomerServiceCustomerAuthenticationResponse CustomerAuthentication(CustomerServiceAuthenticationRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                using (RadiusREntities db = new RadiusREntities())
                {
                    if (request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                    {
                        var CustomerCode = request.AuthenticationParameters.CustomerCode;
                        var dbClients = db.Subscriptions.Where(sub => (sub.Customer.CustomerIDCard.TCKNo == CustomerCode) || sub.Customer.ContactPhoneNo == CustomerCode).ToList();
                        var PassiveSubscriptions = dbClients.Where(s => s.State == (short)CustomerState.Cancelled && !s.Bills.Where(b => b.BillStatusID == (short)BillState.Unpaid).Any()).ToList();
                        if (PassiveSubscriptions.Count() == dbClients.Count())
                        {
                            ErrorsLogger.Error(new Exception($"CustomerAuthentication -> cancelled . CustomerCode : {request.AuthenticationParameters.CustomerCode} ."));
                            //Errorslogger.Error($"CustomerAuthentication -> cancelled . CustomerCode : {request.AuthenticationParameters.CustomerCode} . User : {request.Username}");
                            return new CustomerServiceCustomerAuthenticationResponse(passwordHash, request)
                            {
                                CustomerAuthenticationResponse = null,
                                ResponseMessage = CommonResponse.ClientNotFound(request.Culture),
                            };
                        }
                        if (dbClients.Count() > 0)
                        {
                            // if need to send a new password
                            var validPasswordClient = dbClients.FirstOrDefault(client => client.OnlinePassword != null && client.OnlinePasswordExpirationDate != null);
                            if (validPasswordClient == null || validPasswordClient.OnlinePasswordExpirationDate < DateTime.Now)
                            {
                                var randomPassword = new Random().Next(100000, 1000000).ToString("000000");
                                dbClients.ForEach(client => client.OnlinePassword = randomPassword);
                                dbClients.ForEach(client => client.OnlinePasswordExpirationDate = DateTime.Now.Add(CustomerWebsiteSettings.OnlinePasswordDuration));
                                validPasswordClient = dbClients.FirstOrDefault();
                                SMSService SMS = new SMSService();
                                var customerCulture = string.IsNullOrEmpty(validPasswordClient.Customer.Culture) ? "tr-tr" : validPasswordClient.Customer.Culture;
                                var passwordSMS = db.SMSTexts.Where(sms => !sms.IsDisabled && sms.TypeID == (short)SMSType.WebsiteCredentials && sms.Culture == customerCulture).FirstOrDefault();
                                if (passwordSMS == null)
                                {
                                    SMS.SendGenericSMS(validPasswordClient.Customer.ContactPhoneNo, validPasswordClient.Customer.Culture, rawText: string.Format(Localization.Common.ResourceManager.GetString("PasswordSMS", CultureInfo.CreateSpecificCulture(customerCulture)), validPasswordClient.OnlinePassword, CustomerWebsiteSettings.OnlinePasswordDuration.Hours));
                                }
                                else
                                {
                                    var passwordSMSText = passwordSMS.Text.Replace("([onlinePassword])", "{0}");
                                    SMS.SendGenericSMS(validPasswordClient.Customer.ContactPhoneNo, validPasswordClient.Customer.Culture, rawText: string.Format(passwordSMSText, validPasswordClient.OnlinePassword, CustomerWebsiteSettings.OnlinePasswordDuration.Hours));
                                }
                                db.SaveChanges();
                            }
                            return new CustomerServiceCustomerAuthenticationResponse(passwordHash, request)
                            {
                                ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                CustomerAuthenticationResponse = new CustomerAuthenticationResponse()
                                {
                                    SubscriptionCount = dbClients.Count(),
                                    CurrentSubscriptionId = dbClients.FirstOrDefault().ID
                                },
                            };
                        }
                        else
                        {
                            ErrorsLogger.Error(new Exception($"CustomerAuthentication -> Subscription is not found. CustomerCode : {request.AuthenticationParameters.CustomerCode}."));
                            //Errorslogger.Error($"CustomerAuthentication -> Subscription is not found. CustomerCode : {request.AuthenticationParameters.CustomerCode} . User : {request.Username}");
                            return new CustomerServiceCustomerAuthenticationResponse(passwordHash, request)
                            {
                                CustomerAuthenticationResponse = null,
                                ResponseMessage = CommonResponse.ClientNotFound(request.Culture),
                            };
                        }
                    }
                    //Errorslogger.Error($"CustomerAuthentication unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceCustomerAuthenticationResponse(passwordHash, request)
                    {
                        CustomerAuthenticationResponse = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                    };
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceCustomerAuthenticationResponse(passwordHash, request)
                {
                    CustomerAuthenticationResponse = null,
                    ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceCustomerAuthenticationResponse(passwordHash, request)
                {

                    CustomerAuthenticationResponse = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public CustomerServiceDeactivateAutomaticPaymentResponse DeactivateAutomaticPayment(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceDeactivateAutomaticPaymentResponse(passwordHash, request)
                    {

                        IsSuccess = false,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),

                    };
                }
                if (request.SubscriptionParameters.SubscriptionId == null)
                {
                    return new CustomerServiceDeactivateAutomaticPaymentResponse(passwordHash, request)
                    {


                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        IsSuccess = false
                    };
                }
                using (var db = new RadiusREntities())
                {
                    if (!MobilExpressSettings.MobilExpressIsActive)
                    {
                        return new CustomerServiceDeactivateAutomaticPaymentResponse(passwordHash, request)
                        {

                            IsSuccess = false,

                            ResponseMessage = CommonResponse.MobilexpressIsDeactive(request.Culture)
                        };
                    }
                    var dbSubscription = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId);

                    db.MobilExpressAutoPayments.Remove(dbSubscription.MobilExpressAutoPayment);
                    db.SaveChanges();

                    var client = new SMSService();
                    db.SMSArchives.AddSafely(client.SendSubscriberSMS(dbSubscription, SMSType.MobilExpressDeactivation));
                    db.SystemLogs.Add(SystemLogProcessor.DeactivateAutomaticPayment(dbSubscription.ID, SystemLogInterface.CustomerWebsite, request.Username, "MobilExpress"));
                    db.SaveChanges();

                    return new CustomerServiceDeactivateAutomaticPaymentResponse(passwordHash, request)
                    {


                        IsSuccess = true,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceDeactivateAutomaticPaymentResponse(passwordHash, request)
                {


                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    IsSuccess = false
                };
            }
        }

        //[HttpPost] public CustomerServiceDomainCachesResponse DomainsCaches(CustomerServiceDomainCachesRequest request)
        //{
        //    var password = new ServiceSettings().GetUserPassword(request.Username);
        //    var passwordHash = HashUtilities.GetHexString<SHA1>(password);
        //    try
        //    {
        //        var HasCache = RadiusR.DB.DomainsCache.DomainsCache.HasAnyTelekomDomains;
        //        return new CustomerServiceDomainCachesResponse(passwordHash, request)
        //        {
        //            
        //            
        //            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
        //            Data = HasCache
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        Errorslogger.Error(ex, $" domains cache error -> {ex.Message}");
        //        return new CustomerServiceDomainCachesResponse(passwordHash, request)
        //        {
        //            
        //            
        //            ResponseMessage = CommonResponse.InternalException(request.Culture),
        //            Data = false
        //        };
        //    }
        //}

        [HttpPost]
        public CustomerServiceEArchivePDFResponse EArchivePDF(CustomerServiceEArchivePDFRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                    {
                        //Errorslogger.Error($"EArchivePDF unauthorize error. User : {request.Username}");
                        ErrorsLogger.Error(new Exception("unauthorize error"));
                        return new CustomerServiceEArchivePDFResponse(passwordHash, request)
                        {

                            ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                            EArchivePDFResponse = null,

                        };
                    }
                    var dbBill = db.Bills.Find(request.EArchivePDFParameters.BillId);
                    if (dbBill == null || dbBill.EBill == null || dbBill.EBill.EBillType != (short)EBillType.EArchive)
                    {
                        //Errorslogger.Error($"EArchivePDF -> Bill not found. Bill Id : {request.EArchivePDFParameters.BillId} . User : {request.Username}");
                        ErrorsLogger.Error(new Exception($"EArchivePDF -> Bill not found. Bill Id : {request.EArchivePDFParameters.BillId} ."));
                        return new CustomerServiceEArchivePDFResponse(passwordHash, request)
                        {
                            EArchivePDFResponse = null,
                            ResponseMessage = CommonResponse.BillsNotFoundException(request.Culture)
                        };
                    }
                    if (dbBill.Subscription.ID != request.EArchivePDFParameters.SubscriptionId)
                    {
                        //Errorslogger.Error($"EArchivePDF -> Bill id and subscription id not match. Bill Id : {request.EArchivePDFParameters.BillId} - Subscription Id : {request.EArchivePDFParameters.SubscriptionId}. User : {request.Username}");
                        ErrorsLogger.Error(new Exception($"EArchivePDF -> Bill id and subscription id not match. Bill Id : {request.EArchivePDFParameters.BillId} - Subscription Id : {request.EArchivePDFParameters.SubscriptionId}."));
                        return new CustomerServiceEArchivePDFResponse(passwordHash, request)
                        {
                            EArchivePDFResponse = null,
                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        };
                    }
                    var serviceClient = new RezaB.NetInvoice.Wrapper.NetInvoiceClient(AppSettings.EBillCompanyCode, AppSettings.EBillApiUsername, AppSettings.EBillApiPassword);
                    var response = serviceClient.GetEArchivePDF(dbBill.EBill.ReferenceNo);
                    if (response.PDFData == null)
                    {
                        return new CustomerServiceEArchivePDFResponse(passwordHash, request)
                        {
                            EArchivePDFResponse = null,
                            ResponseMessage = CommonResponse.EArchivePDFNotFound(request.Culture)
                        };
                    }
                    var customerCulture = string.IsNullOrEmpty(dbBill.Subscription.Customer.Culture) ? "tr-tr" : dbBill.Subscription.Customer.Culture;
                    return new CustomerServiceEArchivePDFResponse(passwordHash, request)
                    {
                        EArchivePDFResponse = new EArchivePDFResponse()
                        {
                            FileContent = response.PDFData,
                            ContentType = "application/pdf",
                            FileDownloadName = Localization.Common.ResourceManager.GetString("EArchivePDFFileName", CultureInfo.CreateSpecificCulture(customerCulture)) + "_" + dbBill.IssueDate.ToString("yyyy-MM-dd") + ".pdf"
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceEArchivePDFResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.NullObjectException(request.Culture),
                    EArchivePDFResponse = null,
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceEArchivePDFResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    EArchivePDFResponse = null,
                };
            }
        }

        [HttpPost]
        public CustomerServiceGenericAppSettingsResponse GenericAppSettings(CustomerServiceGenericAppSettingsRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    //Errorslogger.Error($"unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceGenericAppSettingsResponse(passwordHash, request)
                    {
                        GenericAppSettings = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                    };
                }
                var HasCache = RadiusR.DB.DomainsCache.DomainsCache.HasAnyTelekomDomains;
                var MobilexpressIsActive = MobilExpressSettings.MobilExpressIsActive;
                var maxFileCount = RadiusR.DB.CustomerWebsiteSettings.MaxSupportAttachmentPerRequest;
                var maxFileSize = CustomerWebsiteSettings.MaxSupportAttachmentSize;
                var recaptchaSiteKey = CustomerWebsiteSettings.CustomerWebsiteRecaptchaClientKey;
                var recaptchaSecretKey = CustomerWebsiteSettings.CustomerWebsiteRecaptchaServerKey;
                var useGoogleRecaptcha = CustomerWebsiteSettings.CustomerWebsiteUseGoogleRecaptcha;
                return new CustomerServiceGenericAppSettingsResponse(passwordHash, request)
                {
                    GenericAppSettings = new GenericAppSettingsResponse()
                    {
                        FileMaxCount = maxFileCount,
                        FileMaxSize = maxFileSize,
                        HasAnyTelekomDomains = HasCache,
                        MobilExpressIsActive = MobilexpressIsActive,
                        RecaptchaClientKey = recaptchaSiteKey,
                        RecaptchaServerKey = recaptchaSecretKey,
                        UseGoogleRecaptcha = useGoogleRecaptcha
                    },
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGenericAppSettingsResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    GenericAppSettings = null,
                };
            }
        }
        [HttpPost]
        public CustomerServiceGetCustomerBillsResponse GetCustomerBills(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    if (request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                    {
                        var dbClient = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId);
                        if (dbClient == null)
                        {
                            //Errorslogger.Error($"GetCustomerBills -> subscription is not found. Id : {request.SubscriptionParameters.SubscriptionId}. User : {request.Username}");
                            ErrorsLogger.Error(new Exception($"Subscription Id : {request.SubscriptionParameters.SubscriptionId}."));
                            return new CustomerServiceGetCustomerBillsResponse(passwordHash, request)
                            {
                                GetCustomerBillsResponse = null,
                                ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                            };
                        }
                        var firstUnpaidBill = dbClient.Bills.Where(bill => bill.BillStatusID == (short)BillState.Unpaid).OrderBy(bill => bill.IssueDate).FirstOrDefault();
                        var result = dbClient.Bills.OrderByDescending(bill => bill.IssueDate).Select(bill =>
                        new GetCustomerBillsResponse()
                        {
                            PaymentTypeID = bill.PaymentTypeID,
                            BillDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(bill.IssueDate),
                            LastPaymentDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(bill.DueDate),
                            Status = bill.BillStatusID,
                            StatusText = new LocalizedList<RadiusR.DB.Enums.BillState, RadiusR.Localization.Lists.BillState>().GetDisplayText(bill.BillStatusID, CultureInfo.CreateSpecificCulture(request.Culture)),
                            ID = bill.ID,
                            ServiceName = bill.BillFees.Any(bf => bf.FeeTypeID == (short)FeeType.Tariff) ? bill.BillFees.FirstOrDefault(bf => bf.FeeTypeID == (short)FeeType.Tariff).Description : "-",
                            CanBePaid = firstUnpaidBill != null && bill.ID == firstUnpaidBill.ID,
                            HasEArchiveBill = bill.EBill != null && bill.EBill.EBillType == (short)EBillType.EArchive,
                            Total = bill.GetPayableCost(),
                        }
                        );
                        var credits = dbClient.SubscriptionCredits.Select(credit => credit.Amount).DefaultIfEmpty(0m).Sum();
                        return new CustomerServiceGetCustomerBillsResponse(passwordHash, request)
                        {

                            GetCustomerBillsResponse = new GetCustomerBillInfo()
                            {
                                CustomerBills = result.ToArray(),
                                CanHaveQuotaSale = dbClient.Service.CanHaveQuotaSale,
                                HasUnpaidBills = firstUnpaidBill != null,
                                IsPrePaid = !dbClient.HasBilling,
                                SubscriptionCredits = credits
                            },
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                        };
                    }
                    //Errorslogger.Error($"GetCustomerBills unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceGetCustomerBillsResponse(passwordHash, request)
                    {

                        GetCustomerBillsResponse = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),

                    };
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerBillsResponse(passwordHash, request)
                {

                    GetCustomerBillsResponse = null,

                    ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerBillsResponse(passwordHash, request)
                {

                    GetCustomerBillsResponse = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        [HttpPost]
        public CustomerServiceGetCustomerInfoResponse GetCustomerInfo(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    if (request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                    {
                        var dbClient = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId);
                        if (dbClient == null)
                        {
                            ErrorsLogger.Error(new Exception($"Subscription Id : {request.SubscriptionParameters.SubscriptionId}."));
                            //Errorslogger.Error($"GetCustomerInfo -> subscription is not found. Id : {request.SubscriptionParameters.SubscriptionId}. User : {request.Username}");
                            return new CustomerServiceGetCustomerInfoResponse(passwordHash, request)
                            {

                                GetCustomerInfoResponse = null,
                                ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),

                            };
                        }
                        var hasAutoPayment = dbClient.MobilExpressAutoPayment != null;
                        var state = dbClient.State;
                        var stateText = new LocalizedList<RadiusR.DB.Enums.CustomerState, RadiusR.Localization.Lists.CustomerState>().GetDisplayText(state, CultureInfo.CreateSpecificCulture(request.Culture));
                        return new CustomerServiceGetCustomerInfoResponse(passwordHash, request)
                        {

                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            GetCustomerInfoResponse = new GetCustomerInfoResponse()
                            {
                                HashAutoPayment = hasAutoPayment,
                                StaticIP = dbClient.RadiusAuthorization.StaticIP,
                                OnlinePassword = dbClient.OnlinePassword,
                                CustomerState = state,
                                CustomerStateText = stateText,
                                CurrentSubscriberNo = dbClient.SubscriberNo,
                                EMail = dbClient.Customer.Email,
                                PhoneNo = dbClient.Customer.ContactPhoneNo,
                                ValidDisplayName = dbClient.ValidDisplayName,
                                InstallationAddress = dbClient.Address.AddressText,
                                Username = dbClient.RadiusAuthorization.Username,
                                Password = dbClient.RadiusAuthorization.Password,
                                ReferenceNo = dbClient.ReferenceNo,
                                TTSubscriberNo = dbClient.SubscriptionTelekomInfo != null ? dbClient.SubscriptionTelekomInfo.SubscriptionNo : null,
                                PSTN = dbClient.SubscriptionTelekomInfo != null && !string.IsNullOrWhiteSpace(dbClient.SubscriptionTelekomInfo.PSTN) ? dbClient.SubscriptionTelekomInfo.PSTN : null
                            }
                        };
                    }
                    //Errorslogger.Error($"GetCustomerInfo unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceGetCustomerInfoResponse(passwordHash, request)
                    {

                        GetCustomerInfoResponse = null,

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerInfoResponse(passwordHash, request)
                {

                    GetCustomerInfoResponse = null,

                    ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerInfoResponse(passwordHash, request)
                {

                    GetCustomerInfoResponse = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        [HttpPost]
        public CustomerServiceGetCustomerSpecialOffersResponse GetCustomerSpecialOffers(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                var currentSubId = request.SubscriptionParameters.SubscriptionId;
                if (request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    var minDate = DateTime.Now.Date.AddYears(-1);
                    using (RadiusREntities db = new RadiusREntities())
                    {
                        var dbSubscription = db.Subscriptions.Find(currentSubId);
                        var recurringDiscounts = db.RecurringDiscounts.Where(rd => rd.SubscriptionID == dbSubscription.ID).Where(rd => rd.ReferrerRecurringDiscount != null || rd.ReferringRecurringDiscounts.Any())
                            .OrderByDescending(rd => rd.CreationTime).ToArray();
                        var result = recurringDiscounts.Select(rd => new GetCustomerSpecialOffersResponse()
                        {
                            IsCancelled = rd.IsDisabled,
                            EndDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(rd.CreationTime.AddMonths(rd.ApplicationTimes)/*(DateTime)SqlFunctions.DateAdd("month", rd.ApplicationTimes, rd.CreationTime)*/),
                            RemainingCount = rd.IsDisabled ? 0 : rd.ApplicationTimes - (rd.AppliedRecurringDiscounts.Where(ard => ard.ApplicationState == (short)RecurringDiscountApplicationState.Applied).Count()
                                + rd.AppliedRecurringDiscounts.Where(ard => ard.ApplicationState == (short)RecurringDiscountApplicationState.Passed).Count()),
                            StartDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(rd.CreationTime),
                            TotalCount = rd.ApplicationTimes,
                            IsApplicableThisPeriod = (rd.IsDisabled ? 0 : rd.ApplicationTimes - (rd.AppliedRecurringDiscounts.Where(ard => ard.ApplicationState == (short)RecurringDiscountApplicationState.Applied).Count()
                                + rd.AppliedRecurringDiscounts.Where(ard => ard.ApplicationState == (short)RecurringDiscountApplicationState.Passed).Count())) > 0 && !rd.IsDisabled,
                            UsedCount = rd.AppliedRecurringDiscounts.Where(ard => ard.ApplicationState == (short)RecurringDiscountApplicationState.Applied).Count(),
                            MissedCount = rd.AppliedRecurringDiscounts.Where(ard => ard.ApplicationState == (short)RecurringDiscountApplicationState.Passed).Count(),
                            ReferenceNo = rd.ReferrerRecurringDiscount != null ? rd.ReferrerRecurringDiscount.Subscription.ReferenceNo : rd.ReferringRecurringDiscounts.Any() ? rd.ReferringRecurringDiscounts.FirstOrDefault().Subscription.ReferenceNo : null,
                            ReferralSubscriberState = rd.ReferrerRecurringDiscount != null ? rd.ReferrerRecurringDiscount.Subscription.State : rd.ReferringRecurringDiscounts.Any() ? rd.ReferringRecurringDiscounts.FirstOrDefault().Subscription.State : (short?)null,
                        });
                        return new CustomerServiceGetCustomerSpecialOffersResponse(passwordHash, request)
                        {
                            GetCustomerSpecialOffersResponse = result.ToArray(),

                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                        };
                    }
                }
                //Errorslogger.Error($"GetCustomerSpecialOffers unauthorize error. User : {request.Username}");
                ErrorsLogger.Error(new Exception("unauthorize error"));
                return new CustomerServiceGetCustomerSpecialOffersResponse(passwordHash, request)
                {

                    GetCustomerSpecialOffersResponse = null,

                    ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                };
            }
            catch (NullReferenceException ex)
            {
                //ErrorsLogger.Error(ex);
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerSpecialOffersResponse(passwordHash, request)
                {

                    GetCustomerSpecialOffersResponse = null,

                    ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerSpecialOffersResponse(passwordHash, request)
                {

                    GetCustomerSpecialOffersResponse = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        [HttpPost]
        public CustomerServiceGetCustomerTariffAndTrafficInfoResponse GetCustomerTariffAndTrafficInfo(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    using (RadiusREntities db = new RadiusREntities())
                    {
                        var dbSubscription = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId);
                        var monthlyUsage = dbSubscription.RadiusDailyAccountings.GroupBy(daily => daily.Date).OrderByDescending(dailyGroup => dailyGroup.Key).Select(dailyGroup => new
                        {
                            groupingKey = new
                            {
                                year = dailyGroup.Key.Year,
                                month = dailyGroup.Key.Month
                            },
                            download = dailyGroup.Sum(daily => daily.DownloadBytes),
                            upload = dailyGroup.Sum(daily => daily.UploadBytes)
                        }).GroupBy(monthlyGroup => monthlyGroup.groupingKey).Select(monthlyGroup => new
                        {
                            _year = monthlyGroup.Key.year,
                            _month = monthlyGroup.Key.month,
                            Download = monthlyGroup.Sum(dailyGroup => dailyGroup.download),
                            Upload = monthlyGroup.Sum(dailyGroup => dailyGroup.upload)
                        }).OrderByDescending(usage => usage._year).ThenByDescending(usage => usage._month).Take(3).AsQueryable();
                        var monthlyUsageResults = new List<GetCustomerUsageInfoResponse>();
                        foreach (var usageInfo in monthlyUsage)
                        {
                            monthlyUsageResults.Add(new GetCustomerUsageInfoResponse()
                            {
                                Month = usageInfo._month,
                                Year = usageInfo._year,
                                Date = new DateTime(usageInfo._year, usageInfo._month, 1),
                                TotalDownload = usageInfo.Download,
                                TotalUpload = usageInfo.Upload
                            });
                        }
                        var clientUsage = dbSubscription.GetPeriodUsageInfo(dbSubscription.GetCurrentBillingPeriod(ignoreActivationDate: true), db);
                        return new CustomerServiceGetCustomerTariffAndTrafficInfoResponse(passwordHash, request)
                        {

                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            GetCustomerTariffAndTrafficInfoResponse = new GetCustomerTariffAndTrafficInfoResponse()
                            {
                                Download = clientUsage.Download,
                                ServiceName = dbSubscription.Service.Name,
                                Upload = clientUsage.Upload,
                                MonthlyUsage = monthlyUsageResults,
                                BaseQuota = dbSubscription.Service.BaseQuota
                            },

                        };
                    }
                }
                //Errorslogger.Error($"GetCustomerTariffAndTrafficInfo unauthorize error. User : {request.Username}");
                ErrorsLogger.Error(new Exception("unauthorize error"));
                return new CustomerServiceGetCustomerTariffAndTrafficInfoResponse(passwordHash, request)
                {

                    GetCustomerTariffAndTrafficInfoResponse = null,

                    ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                };
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerTariffAndTrafficInfoResponse(passwordHash, request)
                {

                    GetCustomerTariffAndTrafficInfoResponse = null,

                    ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerTariffAndTrafficInfoResponse(passwordHash, request)
                {

                    GetCustomerTariffAndTrafficInfoResponse = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        [HttpPost]
        public CustomerServiceSupportDetailMessagesResponse GetSupportDetailMessages(CustomerServiceSupportDetailMessagesRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    //Errorslogger.Error($"GetSupportDetailMessages unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceSupportDetailMessagesResponse(passwordHash, request)
                    {

                        SupportDetailMessagesResponse = null,

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.SupportDetailMessagesParameters.SubscriptionId == null || request.SupportDetailMessagesParameters.SupportId == null)
                {
                    ErrorsLogger.Error(new Exception($"has null objects. [{request.SupportDetailMessagesParameters.SubscriptionId},{request.SupportDetailMessagesParameters.SupportId}]"));
                    //Errorslogger.Error($"GetSupportDetailMessages -> has null objects. [{request.SupportDetailMessagesParameters.SubscriptionId},{request.SupportDetailMessagesParameters.SupportId}] . User : {request.Username}");
                    return new CustomerServiceSupportDetailMessagesResponse(passwordHash, request)
                    {
                        SupportDetailMessagesResponse = null,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    var SupportProgress = db.SupportRequests.Find(request.SupportDetailMessagesParameters.SupportId);
                    if (SupportProgress != null && SupportProgress.SubscriptionID == request.SupportDetailMessagesParameters.SubscriptionId && SupportProgress.IsVisibleToCustomer)
                    {
                        return new CustomerServiceSupportDetailMessagesResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            SupportDetailMessagesResponse = new SupportDetailMessagesResponse()
                            {
                                CustomerApprovalDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(SupportProgress.CustomerApprovalDate),
                                State = new SupportDetailMessagesResponse.StateType()
                                {
                                    StateId = SupportProgress.StateID,
                                    StateName = RadiusR.Localization.Lists.SupportRequests.SupportRequestStateID.ResourceManager.GetString(((RadiusR.DB.Enums.SupportRequests.SupportRequestStateID)SupportProgress.StateID).ToString(), CultureInfo.CreateSpecificCulture(request.Culture))
                                },
                                SupportDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(SupportProgress.Date),
                                SupportNo = SupportProgress.SupportPin,
                                SupportRequestName = SupportProgress.SupportRequestType.Name,
                                SupportRequestSubName = SupportProgress.SupportRequestSubType.Name,
                                SupportMessages = SupportProgress.SupportRequestProgresses.Where(s => s.IsVisibleToCustomer).OrderByDescending(s => s.Date).Select(s => new SupportDetailMessagesResponse.SupportMessageList()
                                {
                                    IsCustomer = s.AppUserID == null,
                                    Message = s.Message,
                                    MessageDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(s.Date),
                                    StageId = s.ID
                                }),
                                ID = request.SupportDetailMessagesParameters.SupportId.Value,
                                SupportRequestDisplayType = new SupportDetailMessagesResponse.SupportRequestDisplay()
                                {
                                    SupportRequestDisplayTypeId = SupportUtilities.SupportRequestDisplayState(request.SupportDetailMessagesParameters.SubscriptionId.Value, request.SupportDetailMessagesParameters.SupportId.Value).Item2,
                                    SupportRequestDisplayTypeName = SupportUtilities.SupportRequestDisplayState(request.SupportDetailMessagesParameters.SubscriptionId.Value, request.SupportDetailMessagesParameters.SupportId.Value).Item1
                                }
                            },
                        };
                    }
                    else
                    {
                        return new CustomerServiceSupportDetailMessagesResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            SupportDetailMessagesResponse = new SupportDetailMessagesResponse(),
                        };
                    }
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceSupportDetailMessagesResponse(passwordHash, request)
                {
                    SupportDetailMessagesResponse = null,
                    ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceSupportDetailMessagesResponse(passwordHash, request)
                {
                    SupportDetailMessagesResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        [HttpPost]
        public CustomerServiceGetCustomerSupportListResponse GetSupportList(CustomerServiceGetSupportListRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    //Errorslogger.Error($"GetSupportList unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceGetCustomerSupportListResponse(passwordHash, request)
                    {

                        GetCustomerSupportListResponse = null,

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    var tempSupportList = request.GetSupportList.RowCount != null ? db.SupportRequests.Where(s => s.IsVisibleToCustomer == true && s.SubscriptionID == request.GetSupportList.SubscriptionId).OrderByDescending(s => s.Date).Take(request.GetSupportList.RowCount.GetValueOrDefault(10)).ToArray()
                        : db.SupportRequests.Where(s => s.IsVisibleToCustomer == true && s.SubscriptionID == request.GetSupportList.SubscriptionId).OrderByDescending(s => s.Date).ToArray();
                    var supportRequestList = tempSupportList.Select(s => new GetCustomerSupportListResponse()
                    {
                        ApprovalDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(s.CustomerApprovalDate),
                        Date = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(s.Date),
                        State = s.StateID,
                        StateText = "",
                        SupportNo = s.SupportPin,
                        SupportRequestSubType = s.SupportRequestSubType.Name,
                        SupportRequestType = s.SupportRequestType.Name,
                        ID = s.ID
                    }).ToList();
                    foreach (var item in supportRequestList)
                    {
                        item.StateText = new LocalizedList<RadiusR.DB.Enums.SupportRequests.SupportRequestStateID, RadiusR.Localization.Lists.SupportRequests.SupportRequestStateID>()
                            .GetDisplayText(item.State, CultureInfo.CreateSpecificCulture(request.Culture));
                    }
                    return new CustomerServiceGetCustomerSupportListResponse(passwordHash, request)
                    {

                        GetCustomerSupportListResponse = supportRequestList.ToArray(),

                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerSupportListResponse(passwordHash, request)
                {

                    GetCustomerSupportListResponse = null,

                    ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerSupportListResponse(passwordHash, request)
                {

                    GetCustomerSupportListResponse = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public CustomerServiceNameValuePair GetSupportSubTypes(CustomerServiceSupportSubTypesRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    using (var db = new RadiusR.DB.RadiusREntities())
                    {
                        var types = db.SupportRequestSubTypes
                               .Where(m => m.SupportRequestTypeID == request.SupportSubTypesParameters.SupportTypeID && m.IsDisabled == request.SupportSubTypesParameters.IsDisabled).Select(m => new { Name = m.Name, Value = m.ID }).ToArray();
                        return new CustomerServiceNameValuePair(passwordHash, request)
                        {

                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            ValueNamePairList = types.Select(m => new ValueNamePair()
                            {
                                Name = m.Name,
                                Value = m.Value
                            }).ToArray(),

                        };
                    }
                }
                //Errorslogger.Error($"GetSupportSubTypes unauthorize error. User : {request.Username}");
                ErrorsLogger.Error(new Exception("unauthorize error"));
                return new CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = null,

                    ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                };
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = null,

                    ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        [HttpPost]
        public CustomerServiceNameValuePair GetSupportTypes(CustomerServiceSupportTypesRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    using (var db = new RadiusR.DB.RadiusREntities())
                    {
                        var types = db.SupportRequestTypes.Where(m => m.IsStaffOnly == request.SupportTypesParameters.IsStaffOnly && m.IsDisabled == request.SupportTypesParameters.IsDisabled).Select(m => new { Value = m.ID, Name = m.Name }).ToArray();
                        return new CustomerServiceNameValuePair(passwordHash, request)
                        {

                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            ValueNamePairList = types.Select(m => new ValueNamePair()
                            {
                                Name = m.Name,
                                Value = m.Value
                            }).ToArray(),

                        };
                    }
                }
                //Errorslogger.Error($"GetSupportTypes unauthorize error. User : {request.Username}");
                ErrorsLogger.Error(new Exception("unauthorize error"));
                return new CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = null,

                    ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                };
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = null,

                    ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceNameValuePair(passwordHash, request)
                {
                    ValueNamePairList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        [HttpPost]
        public CustomerServiceVPOSFormResponse GetVPOSForm(CustomerServiceVPOSFormRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    //Errorslogger.Error($"GetVPOSForm unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceVPOSFormResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),

                        VPOSFormResponse = null
                    };
                }
                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbSubscription = db.Subscriptions.Find(request.VPOSFormParameters.SubscriptionId);
                    if (dbSubscription == null)
                    {
                        ErrorsLogger.Error(new Exception($"Subscription is not found. subscriptionId : {request.VPOSFormParameters.SubscriptionId}."));
                        //Errorslogger.Error($"GetVPOSForm -> Subscription is not found. subscriptionId : {request.VPOSFormParameters.SubscriptionId} . User : {request.Username}");
                        return new CustomerServiceVPOSFormResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                            VPOSFormResponse = null,
                        };
                    }
                    if (request.VPOSFormParameters.PayableAmount == null)
                    {
                        return new CustomerServiceVPOSFormResponse(passwordHash, request)
                        {
                            VPOSFormResponse = null,
                            ResponseMessage = CommonResponse.NullObjectException(request.Culture),
                        };
                    }
                    var VPOSModel = VPOSManager.GetVPOSModel(
                        request.VPOSFormParameters.OkUrl,
                        request.VPOSFormParameters.FailUrl,
                        request.VPOSFormParameters.PayableAmount.Value,
                        dbSubscription.Customer.Culture.Split('-').FirstOrDefault(),
                        dbSubscription.SubscriberNo + "-" + dbSubscription.ValidDisplayName);
                    var htmlForm = VPOSModel.GetHtmlForm().ToHtmlString();
                    return new CustomerServiceVPOSFormResponse(passwordHash, request)
                    {
                        VPOSFormResponse = new VPOSFormResponse()
                        {
                            HtmlForm = htmlForm,
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceVPOSFormResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                    VPOSFormResponse = null
                };
            }
        }
        [HttpPost]
        public CustomerServicePayBillsResponse PayBills(CustomerServicePayBillsRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    //Errorslogger.Error($"PayBills unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServicePayBillsResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),

                        PayBillsResponse = null,
                    };
                }
                using (RadiusREntities db = new RadiusREntities())
                {
                    if (request.PayBillsParameters.SubscriptionPaidType == null)
                    {
                        return new CustomerServicePayBillsResponse(passwordHash, request)
                        {

                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.ErrorMessages.PaidTypeNull),

                            PayBillsResponse = null,
                        };
                    }
                    if (request.PayBillsParameters.AccountantType == null || request.PayBillsParameters.PaymentType == null)
                    {
                        return new CustomerServicePayBillsResponse(passwordHash, request)
                        {

                            ResponseMessage = CommonResponse.FailedResponse(request.Culture,
                            $"{Localization.ErrorMessages.NullObjectFound} - AccountantType : {request.PayBillsParameters.AccountantType} - PaymentType : {request.PayBillsParameters.PaymentType} "),

                            PayBillsResponse = null,
                        };
                    }
                    if (request.PayBillsParameters.SubscriptionPaidType == (short)SubscriptionPaidType.PrePaid)
                    {
                        if (request.PayBillsParameters.SubscriptionId == null)
                        {
                            return new CustomerServicePayBillsResponse(passwordHash, request)
                            {

                                ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),

                                PayBillsResponse = null,
                            };
                        }
                        var dbSubscription = db.Subscriptions.Find(request.PayBillsParameters.SubscriptionId);
                        var payResponse = RadiusR.DB.Utilities.Billing.ExtendPackage.ExtendClientPackage(db, dbSubscription, 1, (PaymentType)request.PayBillsParameters.PaymentType, (BillPayment.AccountantType)request.PayBillsParameters.AccountantType);
                        db.SystemLogs.Add(RadiusR.SystemLogs.SystemLogProcessor.ExtendPackage(null, dbSubscription.ID, SystemLogInterface.CustomerWebsite, request.Username, 1));
                        db.SaveChanges();
                        if (payResponse == BillPayment.ResponseType.Success)
                        {
                            return new CustomerServicePayBillsResponse(passwordHash, request)
                            {
                                PayBillsResponse = new PayBillsResponse()
                                {
                                    PaymentResponse = CommonResponse.SuccessResponse(request.Culture).ErrorMessage
                                },
                                ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                            };
                        }
                        return new CustomerServicePayBillsResponse(passwordHash, request)
                        {
                            PayBillsResponse = new PayBillsResponse()
                            {
                                PaymentResponse = CommonResponse.PaymentResponse(request.Culture, payResponse).ErrorMessage
                            },
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                        };
                    }
                    else
                    {
                        if (request.PayBillsParameters.BillIds == null)
                        {
                            //Errorslogger.Error($"PayBills -> Bills are empty. User : {request.Username}");
                            ErrorsLogger.Error(new Exception($"Bills are empty"));
                            return new CustomerServicePayBillsResponse(passwordHash, request)
                            {
                                PayBillsResponse = null,
                                ResponseMessage = CommonResponse.BillsNotFoundException(request.Culture)
                            };
                        }
                        if (request.PayBillsParameters.BillIds.Count() == 0)
                        {
                            return new CustomerServicePayBillsResponse(passwordHash, request)
                            {
                                PayBillsResponse = null,
                                ResponseMessage = CommonResponse.BillsNotFoundException(request.Culture)
                            };
                        }
                        var Bills = db.Bills.Where(bill => request.PayBillsParameters.BillIds.Contains(bill.ID)).ToArray();
                        var payResponse = RadiusR.DB.Utilities.Billing.BillPayment.PayBills(db, Bills, (PaymentType)request.PayBillsParameters.PaymentType, (BillPayment.AccountantType)request.PayBillsParameters.AccountantType);
                        db.SystemLogs.Add(RadiusR.SystemLogs.SystemLogProcessor.BillPayment(Bills.Select(b => b.ID).ToArray(), null, Bills.FirstOrDefault().SubscriptionID, SystemLogInterface.CustomerWebsite, request.Username, (PaymentType)request.PayBillsParameters.PaymentType));
                        db.SaveChanges();
                        if (payResponse == BillPayment.ResponseType.Success)
                        {
                            return new CustomerServicePayBillsResponse(passwordHash, request)
                            {
                                PayBillsResponse = new PayBillsResponse()
                                {
                                    PaymentResponse = CommonResponse.SuccessResponse(request.Culture).ErrorMessage
                                },
                                ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                            };
                        }
                        return new CustomerServicePayBillsResponse(passwordHash, request)
                        {
                            PayBillsResponse = new PayBillsResponse()
                            {
                                PaymentResponse = CommonResponse.SuccessResponse(request.Culture).ErrorMessage
                            },
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServicePayBillsResponse(passwordHash, request)
                {
                    PayBillsResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),

                };
            }
        }
        [HttpPost]
        public CustomerServicePaymentTypeListResponse PaymentTypeList(CustomerServicePaymentTypeListRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    //Errorslogger.Error($"unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServicePaymentTypeListResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),

                        PaymentTypes = null
                    };
                }
                var paymentTypes = new RezaB.Data.Localization.LocalizedList<AutoPaymentType, RadiusR.Localization.Lists.AutoPaymentType>().GetList(CultureInfo.CreateSpecificCulture(request.Culture));
                return new CustomerServicePaymentTypeListResponse(passwordHash, request)
                {

                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                    PaymentTypes = paymentTypes.Select(p => new ValueNamePair() { Value = p.Key, Name = p.Value }).ToArray()
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServicePaymentTypeListResponse(passwordHash, request)
                {

                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                    PaymentTypes = null
                };
            }
        }

        [HttpPost]
        public CustomerServiceQuotaPackagesResponse QuotaPackageList(CustomerServiceQuotaPackagesRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    //Errorslogger.Error($"QuotaPackageList unauthorize error. User : {request.Username}");
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceQuotaPackagesResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),

                        QuotaPackageListResponse = null
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    var quotaList = db.QuotaPackages.ToArray();
                    if (quotaList == null || quotaList.Count() == 0)
                    {
                        quotaList = null;
                    }
                    return new CustomerServiceQuotaPackagesResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                        QuotaPackageListResponse = quotaList == null ? Enumerable.Empty<QuotaPackageListResponse>() : quotaList.Select(quota => new QuotaPackageListResponse()
                        {
                            Amount = quota.Amount,
                            ID = quota.ID,
                            Name = quota.Name,
                            Price = quota.Price
                        })
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceQuotaPackagesResponse(passwordHash, request)
                {

                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                    QuotaPackageListResponse = null
                };
            }
        }

        [HttpPost]
        public CustomerServiceQuotaSaleResponse QuotaSale(CustomerServiceQuotaSaleRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceQuotaSaleResponse(passwordHash, request)
                    {


                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        IsQuotaSale = false
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    var dbSubscription = db.Subscriptions.Find(request.QuotaSaleParameters.SubscriptionId);
                    if (dbSubscription == null)
                    {
                        ErrorsLogger.Error(new Exception($"Subscription Id : {request.QuotaSaleParameters.SubscriptionId}."));
                        //Errorslogger.Error($"QuotaSale -> Subscriber is not found. Id : {request.QuotaSaleParameters.SubscriptionId} . User : {request.Username}");
                        return new CustomerServiceQuotaSaleResponse(passwordHash, request)
                        {
                            IsQuotaSale = false,


                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture)
                        };
                    }
                    var dbQuota = db.QuotaPackages.Find(request.QuotaSaleParameters.PackageId);
                    if (dbSubscription != null && dbQuota != null && dbSubscription.Service.CanHaveQuotaSale)
                    {
                        var quotaDescription = RezaB.Data.Formating.RateLimitFormatter.ToQuotaDescription(dbQuota.Amount, dbQuota.Name);
                        dbSubscription.SubscriptionQuotas.Add(new SubscriptionQuota()
                        {
                            AddDate = DateTime.Now,
                            Amount = dbQuota.Amount
                        });

                        dbSubscription.Bills.Add(new Bill()
                        {
                            BillFees = new[]
                            {
                                new BillFee()
                                {
                                    InstallmentCount = 1,
                                    CurrentCost = dbQuota.Price,
                                    Fee = new Fee()
                                    {
                                        Date = DateTime.Now.Date,
                                        FeeTypeID = (short)FeeType.Quota,
                                        Description = quotaDescription,
                                        InstallmentBillCount = 1,
                                        Cost = dbQuota.Price,
                                        SubscriptionID = dbSubscription.ID
                                    }
                                }
                            }.ToList(),
                            DueDate = DateTime.Now.Date,
                            IssueDate = DateTime.Now.Date,
                            BillStatusID = (short)BillState.Paid,
                            PaymentTypeID = (short)PaymentType.VirtualPos,
                            Source = (short)BillSources.Manual,
                            PayDate = DateTime.Now
                        });

                        db.SystemLogs.Add(SystemLogs.SystemLogProcessor.AddSubscriptionQuota(null, dbSubscription.ID, SystemLogInterface.CustomerWebsite, request.Username, quotaDescription));

                        SMSService SMSAsync = new SMSService();
                        db.SMSArchives.AddSafely(SMSAsync.SendSubscriberSMS(dbSubscription, SMSType.PaymentDone, new Dictionary<string, object>()
                        {
                            { SMSParamaterRepository.SMSParameterNameCollection.BillTotal, dbQuota.Price }
                        }));
                        db.SaveChanges();
                        return new CustomerServiceQuotaSaleResponse(passwordHash, request)
                        {
                            IsQuotaSale = true,


                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                        };
                    }
                    else
                    {
                        return new CustomerServiceQuotaSaleResponse(passwordHash, request)
                        {
                            IsQuotaSale = true,

                            ResponseMessage = CommonResponse.QuotaNotFound(request.Culture)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceQuotaSaleResponse(passwordHash, request)
                {


                    IsQuotaSale = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                };
            }
        }

        [HttpPost]
        public CustomerServiceRegisteredCardsResponse RegisteredMobilexpressCardList(CustomerServiceRegisteredCardsRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceRegisteredCardsResponse(passwordHash, request)
                    {

                        RegisteredCardList = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),

                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    var dbCustomer = db.Subscriptions.Find(request.RegisteredCardsParameters.SubscriptionId).Customer;
                    var client = new MobilExpressAdapterClient(MobilExpressSettings.MobilExpressMerchantKey, MobilExpressSettings.MobilExpressAPIPassword, new ClientConnectionDetails()
                    {
                        IP = request.RegisteredCardsParameters.HttpContextParameters.UserHostAddress,
                        UserAgent = request.RegisteredCardsParameters.HttpContextParameters.UserAgent
                    });
                    var response = client.GetCards(dbCustomer);
                    if (response.InternalException != null)
                    {
                        //Errorslogger.Error(response.InternalException, $"RegisteredCardList is exception . User : {request.Username}");
                        ErrorsLogger.Error(response.InternalException);
                        return new CustomerServiceRegisteredCardsResponse(passwordHash, request)
                        {
                            RegisteredCardList = null,
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.Common.GeneralError)
                        };
                    }
                    if (response.Response.ResponseCode != RezaB.API.MobilExpress.Response.ResponseCodes.Success)
                    {
                        if (response.Response.ResponseCode != RezaB.API.MobilExpress.Response.ResponseCodes.CardNotFound && response.Response.ResponseCode != RezaB.API.MobilExpress.Response.ResponseCodes.CustomerNotFound)
                        {
                            //Errorslogger.Error($"RegisteredCardList is exception -> {response.Response.ErrorMessage} . User : {request.Username}");
                            ErrorsLogger.Error(new Exception($"RegisteredCardList is exception -> {response.Response.ErrorMessage}."));
                            return new CustomerServiceRegisteredCardsResponse(passwordHash, request)
                            {
                                RegisteredCardList = null,
                                ResponseMessage = CommonResponse.FailedResponse(request.Culture, response.Response.ErrorMessage)
                            };
                        }
                        return new CustomerServiceRegisteredCardsResponse(passwordHash, request)
                        {
                            RegisteredCardList = null,
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                        };
                    }
                    else
                    {
                        return new CustomerServiceRegisteredCardsResponse(passwordHash, request)
                        {
                            RegisteredCardList = response.Response.CardList.Select(cl => new RegisteredCardsResponse()
                            {
                                MaskedCardNo = cl.MaskedCardNumber,
                                HasAutoPayments = false,
                                Token = cl.CardToken,
                            }).ToArray(),
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceRegisteredCardsResponse(passwordHash, request)
                {

                    RegisteredCardList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                };
            }
        }

        [HttpPost]
        public CustomerServiceRemoveCardResponse RemoveCard(CustomerServiceRemoveCardRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceRemoveCardResponse(passwordHash, request)
                    {

                        IsSuccess = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),

                    };
                }
                if (request.RemoveCardParameters.SubscriptionId == null)
                {
                    //Errorslogger.Error($"Calling 'RemoveCard' some parameters are null value. Parameters : [{request.RemoveCardParameters.SubscriptionId}] ");
                    ErrorsLogger.Error(new Exception($"Calling 'RemoveCard' some parameters are null value. Parameters : [{request.RemoveCardParameters.SubscriptionId}] "));
                    return new CustomerServiceRemoveCardResponse(passwordHash, request)
                    {
                        IsSuccess = false,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                    };
                }
                using (var db = new RadiusREntities())
                {
                    //var smsCode = CacheManager.GetKey(request.Username, request.SubscriptionParameters.SubscriptionId.ToString(), CacheTypes.RemoveCardSMSValidation);
                    //if (smsCode != request.Data.SMSCode)
                    //{
                    //    return new CustomerServiceRemoveCardResponse(passwordHash, request)
                    //    {
                    //        
                    //        Data = false,
                    //        
                    //        ResponseMessage = CommonResponse.WrongSMSValidation(request.Culture)
                    //    };
                    //}
                    if (db.MobilExpressAutoPayments.Any(ap => ap.CardToken == request.RemoveCardParameters.CardToken))//automatic payment have to deactive
                    {
                        return new CustomerServiceRemoveCardResponse(passwordHash, request)
                        {


                            IsSuccess = false,
                            ResponseMessage = CommonResponse.HasActiveAutoPayment(request.Culture)
                        };
                    }
                    var dbClient = db.Subscriptions.Find(request.RemoveCardParameters.SubscriptionId);
                    var dbCustomer = dbClient.Customer;
                    var client = new MobilExpressAdapterClient(MobilExpressSettings.MobilExpressMerchantKey, MobilExpressSettings.MobilExpressAPIPassword, new ClientConnectionDetails()
                    {
                        IP = request.RemoveCardParameters.HttpContextParameters.UserHostAddress,
                        UserAgent = request.RemoveCardParameters.HttpContextParameters.UserAgent
                    });
                    // get card info
                    var cards = client.GetCards(dbCustomer);
                    if (cards.InternalException != null)
                    {
                        //Errorslogger.Warn(cards.InternalException, "Error calling 'GetCards' from MobilExpress client");
                        ErrorsLogger.Error(cards.InternalException);
                        return new CustomerServiceRemoveCardResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.Common.GeneralError),
                            IsSuccess = false,
                        };
                    }
                    if (cards.Response.ResponseCode != RezaB.API.MobilExpress.Response.ResponseCodes.Success)
                    {
                        //Errorslogger.Warn("Error calling 'GetCards' from MobilExpress client");
                        ErrorsLogger.Error(new Exception("Error calling 'GetCards' from MobilExpress client"));
                        return new CustomerServiceRemoveCardResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, cards.Response.ErrorMessage),
                            IsSuccess = false,
                        };
                    }
                    var targetCard = cards.Response.CardList.FirstOrDefault(c => c.CardToken == request.RemoveCardParameters.CardToken);

                    // delete card
                    var response = client.DeleteCard(dbCustomer, request.RemoveCardParameters.CardToken);
                    if (response.InternalException != null)
                    {
                        //Errorslogger.Warn(response.InternalException, "Error calling 'DeleteCard' from MobilExpress client");
                        ErrorsLogger.Error(response.InternalException);
                        return new CustomerServiceRemoveCardResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.Common.GeneralError),
                            IsSuccess = false,
                        };
                    }
                    if (response.Response.ResponseCode != RezaB.API.MobilExpress.Response.ResponseCodes.Success)
                    {
                        ErrorsLogger.Error(new Exception($"Error calling 'DeleteCard' from MobilExpress client. Message : {response.Response.ErrorMessage} , Code : {response.Response.ResponseCode}"));
                        //Errorslogger.Warn($"Error calling 'DeleteCard' from MobilExpress client. Message : {response.Response.ErrorMessage} , Code : {response.Response.ResponseCode}");
                        return new CustomerServiceRemoveCardResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, response.Response.ErrorMessage),
                            IsSuccess = false,
                        };
                    }

                    db.SystemLogs.Add(SystemLogProcessor.RemoveCreditCard(dbCustomer.ID, SystemLogInterface.CustomerWebsite, request.Username, targetCard.MaskedCardNumber));
                    db.SaveChanges();
                    return new CustomerServiceRemoveCardResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        IsSuccess = true
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceRemoveCardResponse(passwordHash, request)
                {
                    IsSuccess = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                };
            }
        }

        [HttpPost]
        public CustomerServiceRemoveCardSMSValidationResponse RemoveCardSMSCheck(CustomerServiceRemoveCardSMSCheckRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceRemoveCardSMSValidationResponse(passwordHash, request)
                    {

                        SMSCode = null,

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbSubscription = db.Subscriptions.Find(request.RemoveCardSMSCheckParameters.SubscriptionId);
                    if (db.MobilExpressAutoPayments.Any(ap => ap.CardToken == request.RemoveCardSMSCheckParameters.CardToken))
                    {
                        return new CustomerServiceRemoveCardSMSValidationResponse(passwordHash, request)
                        {
                            SMSCode = null,
                            ResponseMessage = CommonResponse.HasActiveAutoPayment(request.Culture)
                        };
                    }
                    var rand = new Random();
                    var smsCode = rand.Next(100000, 1000000).ToString();
                    var smsClient = new SMSService();
                    smsClient.SendSubscriberSMS(dbSubscription, SMSType.MobilExpressAddRemoveCard, new Dictionary<string, object>() {
                        { SMSParamaterRepository.SMSParameterNameCollection.SMSCode, smsCode }
                    });
                    //Errorslogger.Info($"Remove credit card sms code : {smsCode}");
                    ErrorsLogger.Info($"Remove credit card sms code : {smsCode}");
                    return new CustomerServiceRemoveCardSMSValidationResponse(passwordHash, request)
                    {
                        SMSCode = smsCode,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                //Errorslogger.Error(ex, $" message : {ex.Message}{Environment.NewLine} - Subscription Id : {request.RemoveCardSMSCheckParameters.SubscriptionId}{Environment.NewLine} - Card Token : {request.RemoveCardSMSCheckParameters.CardToken}");
                return new CustomerServiceRemoveCardSMSValidationResponse(passwordHash, request)
                {
                    SMSCode = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                };
            }
        }
        [HttpPost]
        public CustomerServiceAutoPaymentListResponse AutoPaymentList(CustomerServiceAutoPaymentListRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceAutoPaymentListResponse(passwordHash, request)
                    {
                        AutoPaymentListResult = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.AutoPaymentListParameters.SubscriptionId == null)
                {
                    return new CustomerServiceAutoPaymentListResponse(passwordHash, request)
                    {
                        AutoPaymentListResult = null,
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture)
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbCustomer = db.Subscriptions.Find(request.AutoPaymentListParameters.SubscriptionId).Customer;
                    var subscriptions = dbCustomer.Subscriptions.Where(s => !s.IsCancelled).ToArray();
                    var cards = request.AutoPaymentListParameters.CardList;
                    var expiredOrInvalidCards = subscriptions.Where(m => m.MobilExpressAutoPayment != null).Select(s => s.MobilExpressAutoPayment).Where(meap => cards != null && !cards.Select(c => c.Token).Contains(meap.CardToken));
                    db.MobilExpressAutoPayments.RemoveRange(expiredOrInvalidCards);
                    db.SaveChanges();

                    var autoPayments = subscriptions.Select(s => new AutoPaymentListResponse()
                    {
                        SubscriberID = s.ID,
                        SubscriberNo = s.SubscriberNo,
                        Cards = s.MobilExpressAutoPayment != null ? new AutoPaymentListResponse.Card
                        {
                            MaskedCardNo = request.AutoPaymentListParameters.CardList.FirstOrDefault(cl => cl.Token == s.MobilExpressAutoPayment.CardToken).MaskedCardNo,
                            Token = s.MobilExpressAutoPayment.CardToken
                        } : null
                    }).ToArray();
                    return new CustomerServiceAutoPaymentListResponse(passwordHash, request)
                    {

                        AutoPaymentListResult = autoPayments,

                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                //Errorslogger.Error(ex, $" message : {ex.Message}{Environment.NewLine} - Subscription Id : {request.AutoPaymentListParameters.SubscriptionId}{Environment.NewLine}");
                return new CustomerServiceAutoPaymentListResponse(passwordHash, request)
                {

                    AutoPaymentListResult = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                };
            }
        }

        [HttpPost]
        public CustomerServiceSendSubscriberSMSResponse SendSubscriberSMS(CustomerServiceSendSubscriberSMSRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceSendSubscriberSMSResponse(passwordHash, request)
                    {

                        SendSubscriberSMSResponse = false,

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.SendSubscriberSMS.SubscriptionPaidType == null)
                {
                    return new CustomerServiceSendSubscriberSMSResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.ErrorMessages.PaidTypeNull),

                        SendSubscriberSMSResponse = null,
                    };
                }
                if (request.SendSubscriberSMS.SubscriptionPaidType == (short)SubscriptionPaidType.PrePaid)
                {
                    if (request.SendSubscriberSMS.SubscriptionId == null)
                    {
                        return new CustomerServiceSendSubscriberSMSResponse(passwordHash, request)
                        {

                            SendSubscriberSMSResponse = false,

                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture)
                        };
                    }
                    using (var db = new RadiusREntities())
                    {
                        var dbSubscription = db.Subscriptions.Find(request.SendSubscriberSMS.SubscriptionId);
                        RadiusR.SMS.SMSService SMSService = new SMSService();
                        //db.ExtendClientPackage(dbSubscription, 1, PaymentType.VirtualPos, BillPayment.AccountantType.Admin);
                        SMSService SMSAsync = new SMSService();
                        db.SMSArchives.AddSafely(SMSAsync.SendSubscriberSMS(dbSubscription, SMSType.ExtendPackage, new Dictionary<string, object>()
                        {
                            { SMSParamaterRepository.SMSParameterNameCollection.ExtendedMonths, "1" }
                        }));
                        db.SystemLogs.Add(SystemLogProcessor.ExtendPackage(null, dbSubscription.ID, SystemLogInterface.CustomerWebsite, request.Username, 1));
                        db.SaveChanges();
                    }
                }
                else
                {
                    if (request.SendSubscriberSMS.SubscriptionId == null || request.SendSubscriberSMS.PayableAmount == null)
                    {
                        return new CustomerServiceSendSubscriberSMSResponse(passwordHash, request)
                        {

                            SendSubscriberSMSResponse = false,

                            ResponseMessage = request.SendSubscriberSMS.PayableAmount == null ? CommonResponse.BillsNotFoundException(request.Culture)
                            : CommonResponse.SubscriberNotFoundErrorResponse(request.Culture)
                        };
                    }
                    using (var db = new RadiusREntities())
                    {
                        var dbSubscription = db.Subscriptions.Find(request.SendSubscriberSMS.SubscriptionId);
                        RadiusR.SMS.SMSService SMSService = new SMSService();
                        db.SMSArchives.AddSafely(SMSService.SendSubscriberSMS(dbSubscription, SMSType.PaymentDone, new Dictionary<string, object>()
                        {
                            { SMSParamaterRepository.SMSParameterNameCollection.BillTotal, request.SendSubscriberSMS.PayableAmount }
                        }));
                        //db.SystemLogs.Add(SystemLogProcessor.BillPayment(request.SendSubscriberSMS.BillIds, null, dbSubscription.ID, SystemLogInterface.CustomerWebsite, dbSubscription.SubscriberNo, PaymentType.VirtualPos));
                        db.SaveChanges();
                    }
                }
                return new CustomerServiceSendSubscriberSMSResponse(passwordHash, request)
                {

                    SendSubscriberSMSResponse = true,

                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                //Errorslogger.Error(ex, $" - subscription Id : {request.SendSubscriberSMS.SubscriptionId}  message : {ex.Message}");
                ErrorsLogger.Error(ex);
                return new CustomerServiceSendSubscriberSMSResponse(passwordHash, request)
                {
                    SendSubscriberSMSResponse = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex)
                };
            }
        }

        [HttpPost]
        public CustomerServiceSendSupportMessageResponse SendSupportMessage(CustomerServiceSendSupportMessageRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                    {
                        SendSupportMessageResponse = false,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.SendSupportMessageParameters.SubscriptionId == null || request.SendSupportMessageParameters.SupportId == null)
                {
                    ErrorsLogger.Error(new Exception($"SendSupportMessage have null objects [{request.SendSupportMessageParameters.SupportId},{request.SendSupportMessageParameters.SubscriptionId}]."));
                    return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                    {
                        SendSupportMessageResponse = false,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        var SupportProgress = db.SupportRequests.Find(request.SendSupportMessageParameters.SupportId);
                        if (SupportProgress != null && SupportProgress.SubscriptionID == request.SendSupportMessageParameters.SubscriptionId && SupportProgress.IsVisibleToCustomer)
                        {
                            if (request.SendSupportMessageParameters.SupportMessageType == (int)SupportMesssageTypes.ProblemSolved)
                            {
                                if (SupportUtilities.SupportRequestAvailable(request.SendSupportMessageParameters.SubscriptionId.Value, request.SendSupportMessageParameters.SupportId.Value) == SupportRequestAvailableTypes.None)
                                {
                                    return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                    {
                                        SendSupportMessageResponse = false,
                                        ResponseMessage = CommonResponse.InvalidOperation(request.Culture)
                                    };
                                }
                                var CurrentState = (RadiusR.DB.Enums.SupportRequests.SupportRequestStateID)SupportProgress.StateID;
                                SupportProgress.CustomerApprovalDate = DateTime.Now;
                                SupportProgress.StateID = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.Done;
                                long? stageId = null;
                                if (CurrentState == RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.Done)
                                {
                                    var currentStage = new RadiusR.DB.SupportRequestProgress()
                                    {
                                        ActionType = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestActionTypes.Create,
                                        Date = DateTime.Now,
                                        IsVisibleToCustomer = true,
                                        Message = request.SendSupportMessageParameters.Message
                                    };
                                    SupportProgress.SupportRequestProgresses.Add(currentStage);
                                    db.SaveChanges();
                                    if (request.SendSupportMessageParameters.Attachments != null)
                                    {
                                        var fileResponse = SaveSupportAttachments(request.SendSupportMessageParameters.Attachments.ToArray(), currentStage.ID, request.SendSupportMessageParameters.SupportId.Value);
                                        if (!fileResponse)
                                        {
                                            return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                            {
                                                ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                                                SendSupportMessageResponse = false
                                            };
                                        }
                                    }

                                    stageId = currentStage.ID;
                                }
                                else
                                {
                                    var currentStage = new RadiusR.DB.SupportRequestProgress()
                                    {
                                        ActionType = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestActionTypes.ChangeState,
                                        Date = DateTime.Now,
                                        IsVisibleToCustomer = true,
                                        Message = request.SendSupportMessageParameters.Message,
                                        OldState = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.InProgress,
                                        NewState = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.Done
                                    };
                                    SupportProgress.SupportRequestProgresses.Add(currentStage);
                                    db.SaveChanges();
                                    // add file 
                                    if (request.SendSupportMessageParameters.Attachments != null)
                                    {
                                        var fileResponse = SaveSupportAttachments(request.SendSupportMessageParameters.Attachments.ToArray(), currentStage.ID, request.SendSupportMessageParameters.SupportId.Value);
                                        if (!fileResponse)
                                        {
                                            transaction.Rollback();
                                            return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                            {
                                                ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                                                SendSupportMessageResponse = false
                                            };
                                        }
                                    }
                                    stageId = currentStage.ID;
                                }
                                transaction.Commit();
                                return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                {
                                    SendSupportMessageResponse = true,
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                                };
                            }
                            if (request.SendSupportMessageParameters.SupportMessageType == (int)SupportMesssageTypes.OpenRequestAgain)
                            {
                                if (SupportUtilities.HasOpenRequest(request.SendSupportMessageParameters.SubscriptionId.Value))
                                {
                                    return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                    {

                                        SendSupportMessageResponse = true,
                                        ResponseMessage = CommonResponse.HasActiveRequest(request.Culture),

                                    };
                                }
                                if (SupportUtilities.SupportRequestAvailable(request.SendSupportMessageParameters.SubscriptionId.Value, request.SendSupportMessageParameters.SupportId.Value) == SupportRequestAvailableTypes.None)
                                {
                                    return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                    {
                                        SendSupportMessageResponse = true,
                                        ResponseMessage = CommonResponse.InvalidOperation(request.Culture)
                                    };
                                }
                                SupportProgress.StateID = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.InProgress;
                                SupportProgress.AssignedGroupID = null;
                                SupportProgress.RedirectedGroupID = null;
                                var currentStage = new RadiusR.DB.SupportRequestProgress()
                                {
                                    ActionType = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestActionTypes.ChangeState,
                                    Date = DateTime.Now,
                                    IsVisibleToCustomer = true,
                                    Message = request.SendSupportMessageParameters.Message,
                                    OldState = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.Done,
                                    NewState = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.InProgress
                                };
                                SupportProgress.SupportRequestProgresses.Add(currentStage);
                                db.SaveChanges();
                                if (request.SendSupportMessageParameters.Attachments != null)
                                {
                                    var fileResponse = SaveSupportAttachments(request.SendSupportMessageParameters.Attachments.ToArray(), currentStage.ID, request.SendSupportMessageParameters.SupportId.Value);
                                    if (!fileResponse)
                                    {
                                        transaction.Rollback();
                                        return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                        {
                                            ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                                            SendSupportMessageResponse = false
                                        };
                                    }
                                }
                                var stageId = currentStage.ID;
                                transaction.Commit();
                                return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                    SendSupportMessageResponse = true,
                                };
                            }
                            if (request.SendSupportMessageParameters.SupportMessageType == (int)SupportMesssageTypes.AddNote)
                            {
                                long? stageId = null;
                                if (SupportUtilities.SupportRequestAvailable(request.SendSupportMessageParameters.SubscriptionId.Value, request.SendSupportMessageParameters.SupportId.Value) == SupportRequestAvailableTypes.None)
                                {
                                    return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                    {
                                        SendSupportMessageResponse = false,
                                        ResponseMessage = CommonResponse.InvalidOperation(request.Culture)
                                    };
                                }
                                var currentStage = new RadiusR.DB.SupportRequestProgress()
                                {
                                    ActionType = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestActionTypes.Create,
                                    Date = DateTime.Now,
                                    IsVisibleToCustomer = true,
                                    Message = request.SendSupportMessageParameters.Message
                                };
                                SupportProgress.SupportRequestProgresses.Add(currentStage);
                                db.SaveChanges();
                                if (request.SendSupportMessageParameters.Attachments != null)
                                {
                                    var fileResponse = SaveSupportAttachments(request.SendSupportMessageParameters.Attachments.ToArray(), currentStage.ID, request.SendSupportMessageParameters.SupportId.Value);
                                    if (!fileResponse)
                                    {
                                        transaction.Rollback();
                                        return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                        {
                                            ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                                            SendSupportMessageResponse = false
                                        };
                                    }
                                }
                                stageId = currentStage.ID;
                                transaction.Commit();
                                return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                    SendSupportMessageResponse = true,
                                };
                            }
                        }
                        return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                            SendSupportMessageResponse = false
                        };
                    }
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.NullObjectException(request.Culture),
                    SendSupportMessageResponse = false
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceSendSupportMessageResponse(passwordHash, request)
                {
                    SendSupportMessageResponse = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public CustomerServiceSubscriptionBasicInformationResponse SubscriptionBasicInfo(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceSubscriptionBasicInformationResponse(passwordHash, request)
                    {
                        SubscriptionBasicInformationResponse = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.SubscriptionParameters.SubscriptionId == null)
                {
                    return new CustomerServiceSubscriptionBasicInformationResponse(passwordHash, request)
                    {
                        SubscriptionBasicInformationResponse = null,

                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),

                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbClient = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId.Value);
                    var customerId = dbClient.Customer.ID;
                    var IsCancelled = dbClient.IsCancelled;
                    var dbCustomers = db.Customers.Where(c => c.CustomerIDCard.TCKNo == dbClient.Customer.CustomerIDCard.TCKNo || c.ContactPhoneNo == dbClient.Customer.ContactPhoneNo).ToArray();
                    var relatedCustomers = GetRelatedCustomers(dbCustomers); //dbCustomers.SelectMany(c => c.Subscriptions).Select(s => s.ID + "," + s.SubscriberNo);
                    var ID = dbClient.ID;
                    var SubscriberNo = dbClient.SubscriberNo;
                    var ValidDisplayName = dbClient.ValidDisplayName;
                    var relatedCustomerList = relatedCustomers.ToList();
                    var hasBilling = dbClient.HasBilling;
                    var servicePrice = dbClient.Service.Price;

                    return new CustomerServiceSubscriptionBasicInformationResponse(passwordHash, request)
                    {

                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                        SubscriptionBasicInformationResponse = new SubscriptionBasicInformationResponse()
                        {
                            ID = ID,
                            SubscriberNo = SubscriberNo,
                            ValidDisplayName = ValidDisplayName,
                            RelatedCustomers = relatedCustomerList,
                            HasBilling = hasBilling,
                            CustomerID = customerId,
                            IsCancelled = IsCancelled,
                            SubscriptionService = new SubscriptionBasicInformationResponse.Service()
                            {
                                Price = servicePrice
                            }
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceSubscriptionBasicInformationResponse(passwordHash, request)
                {

                    SubscriptionBasicInformationResponse = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public CustomerServiceHasActiveRequestResponse SupportHasActiveRequest(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceHasActiveRequestResponse(passwordHash, request)
                    {

                        HasActiveRequest = true,

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                    };
                }
                if (request.SubscriptionParameters.SubscriptionId == null)
                {
                    return new CustomerServiceHasActiveRequestResponse(passwordHash, request)
                    {


                        HasActiveRequest = true,
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                    };
                }
                return new CustomerServiceHasActiveRequestResponse(passwordHash, request)
                {


                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                    HasActiveRequest = SupportUtilities.HasOpenRequest(request.SubscriptionParameters.SubscriptionId.Value)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceHasActiveRequestResponse(passwordHash, request)
                {

                    HasActiveRequest = true,

                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                };
            }
        }

        [HttpPost]
        public CustomerServiceSupportRegisterResponse SupportRegister(CustomerServiceSupportRegisterRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceSupportRegisterResponse(passwordHash, request)
                    {

                        SupportRegisterResponse = null,

                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.SupportRegisterParameters.RequestTypeId == null || request.SupportRegisterParameters.SubRequestTypeId == null || request.SupportRegisterParameters.SubscriptionId == null)
                {
                    //Errorslogger.Error($"SupportRegister have null objects [{request.SupportRegisterParameters.RequestTypeId},{request.SupportRegisterParameters.SubRequestTypeId},{request.SupportRegisterParameters.SubscriptionId}]. User : {request.Username}");
                    ErrorsLogger.Error(new Exception($"SupportRegister have null objects [{request.SupportRegisterParameters.RequestTypeId},{request.SupportRegisterParameters.SubRequestTypeId},{request.SupportRegisterParameters.SubscriptionId}]."));
                    return new CustomerServiceSupportRegisterResponse(passwordHash, request)
                    {
                        SupportRegisterResponse = null,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),
                    };
                }
                if (SupportUtilities.HasOpenRequest(request.SupportRegisterParameters.SubscriptionId.Value))
                {
                    return new CustomerServiceSupportRegisterResponse(passwordHash, request)
                    {
                        SupportRegisterResponse = null,
                        ResponseMessage = CommonResponse.HasActiveRequest(request.Culture)
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        var result = db.SupportRequests.Add(new SupportRequest()
                        {
                            Date = DateTime.Now,
                            IsVisibleToCustomer = true,
                            StateID = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.InProgress,
                            TypeID = request.SupportRegisterParameters.RequestTypeId.Value,
                            SubTypeID = request.SupportRegisterParameters.SubRequestTypeId.Value,
                            SupportPin = RadiusR.DB.RandomCode.CodeGenerator.GenerateSupportRequestPIN(),
                            SubscriptionID = request.SupportRegisterParameters.SubscriptionId,
                            SupportRequestProgresses =
                        {
                            new RadiusR.DB.SupportRequestProgress()
                            {
                                Date = DateTime.Now,
                                IsVisibleToCustomer = true,
                                Message = request.SupportRegisterParameters.Description.Trim(new char[]{ ' ' , '\n' , '\r' }),
                                ActionType = (short)RadiusR.DB.Enums.SupportRequests.SupportRequestActionTypes.Create
                            }
                        }
                        });
                        db.SaveChanges();
                        if (request.SupportRegisterParameters.Attachments != null)
                        {
                            var fileResponse = SaveSupportAttachments(request.SupportRegisterParameters.Attachments.ToArray(), result.SupportRequestProgresses.FirstOrDefault().ID, result.ID);
                            if (!fileResponse)
                            {
                                transaction.Rollback();
                                return new CustomerServiceSupportRegisterResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                                    SupportRegisterResponse = new SupportRegisterResponse()
                                    {
                                        SupportRegisterResult = false
                                    }
                                };
                            }
                        }
                        transaction.Commit();
                        return new CustomerServiceSupportRegisterResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            SupportRegisterResponse = new SupportRegisterResponse()
                            {
                                SupportRegisterResult = true
                            }
                        };
                    }
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceSupportRegisterResponse(passwordHash, request)
                {
                    SupportRegisterResponse = null,
                    ResponseMessage = CommonResponse.NullObjectException(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceSupportRegisterResponse(passwordHash, request)
                {
                    SupportRegisterResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public CustomerServiceSupportStatusResponse SupportStatus(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceSupportStatusResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        SupportStatusResponse = null,
                    };
                }
                if (request.SubscriptionParameters.SubscriptionId == null)
                {
                    return new CustomerServiceSupportStatusResponse(passwordHash, request)
                    {


                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        SupportStatusResponse = null,
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    var PassedTimeSpan = CustomerWebsiteSettings.SupportRequestPassedTime;
                    var subscriptionId = request.SubscriptionParameters.SubscriptionId;
                    var CurrentSupportProgress = db.SupportRequestProgresses.Where(m => m.SupportRequest.SubscriptionID == subscriptionId && m.SupportRequest.IsVisibleToCustomer).OrderByDescending(m => m.Date).FirstOrDefault();
                    var IsPassedTime = false;
                    if (CurrentSupportProgress != null && CurrentSupportProgress.SupportRequest.StateID == (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.Done && ((DateTime.Now - CurrentSupportProgress.Date) > PassedTimeSpan))
                    {
                        IsPassedTime = true;
                    }
                    var SupportRequests = db.SupportRequests.OrderByDescending(m => m.Date).Where(m => m.SubscriptionID == subscriptionId && m.IsVisibleToCustomer).FirstOrDefault();
                    var IsAppUser = SupportRequests == null ? false : SupportRequests.SupportRequestProgresses.Where(m => m.IsVisibleToCustomer).OrderByDescending(m => m.Date).FirstOrDefault().AppUserID != null ? true : false;
                    var count = 0;
                    // will send stage id from support request 
                    // change from 'SupportRequests.ID' to 'stageId'

                    //List<long> requestIds = new List<long>();
                    long? stageId = null;
                    if (SupportRequests != null && IsAppUser && !IsPassedTime && SupportRequests.CustomerApprovalDate == null)
                    {
                        var selectedStageId = SupportRequests.SupportRequestProgresses.OrderByDescending(s => s.Date).FirstOrDefault();
                        //requestIds.Add(SupportRequests.ID);
                        stageId = selectedStageId == null ? null : selectedStageId.ID;
                        count = 1;
                    }
                    return new CustomerServiceSupportStatusResponse(passwordHash, request)
                    {


                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        SupportStatusResponse = new SupportStatusResponse()
                        {
                            Count = count,
                            StageId = stageId
                            //SupportRequestIds = requestIds
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceSupportStatusResponse(passwordHash, request)
                {
                    SupportStatusResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public CustomerServicePaymentSystemLogResponse PaymentSystemLog(CustomerServicePaymentSystemLogRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServicePaymentSystemLogResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        PaymentSystemLogResult = false,
                    };
                }
                if (request.PaymentSystemLogParameters.SubscriptionId == null)
                {
                    return new CustomerServicePaymentSystemLogResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        PaymentSystemLogResult = false
                    };
                }
                if (request.PaymentSystemLogParameters.PaymentType == null || string.IsNullOrEmpty(request.PaymentSystemLogParameters.SubscriberNo))
                {
                    return new CustomerServicePaymentSystemLogResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),

                        PaymentSystemLogResult = false,
                    };
                }
                // log - need web service
                using (var db = new RadiusREntities())
                {
                    var subscriptionId = request.PaymentSystemLogParameters.SubscriptionId;
                    db.SystemLogs.Add(SystemLogProcessor.BillPayment(request.PaymentSystemLogParameters.BillIds, null, subscriptionId.Value, SystemLogInterface.CustomerWebsite, request.Username, (PaymentType)request.PaymentSystemLogParameters.PaymentType));
                    db.SaveChanges();
                    return new CustomerServicePaymentSystemLogResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        PaymentSystemLogResult = true,
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServicePaymentSystemLogResponse(passwordHash, request)
                {
                    PaymentSystemLogResult = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public CustomerServiceMobilexpressPayBillResponse MobilexpressPayBill(CustomerServiceMobilexpressPayBillRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceMobilexpressPayBillResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        MobilexpressPayBillResult = false,
                    };
                }
                if (request.MobilexpressPayBillParameters.SubscriptionId == null)
                {
                    return new CustomerServiceMobilexpressPayBillResponse(passwordHash, request)
                    {


                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        MobilexpressPayBillResult = false,
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbSubscription = db.Subscriptions.Find(request.MobilexpressPayBillParameters.SubscriptionId);
                    var dbCustomer = dbSubscription.Customer;
                    var token = request.MobilexpressPayBillParameters.Token;
                    var payableAmount = request.MobilexpressPayBillParameters.PayableAmount ?? 0m;
                    var dbBills = dbSubscription.Bills.Where(b => b.BillStatusID == (short)BillState.Unpaid).ToList();
                    if (request.MobilexpressPayBillParameters.BillId.HasValue)
                        dbBills = dbBills.Where(b => b.ID == request.MobilexpressPayBillParameters.BillId).ToList();

                    var client = new MobilExpressAdapterClient(MobilExpressSettings.MobilExpressMerchantKey, MobilExpressSettings.MobilExpressAPIPassword, new ClientConnectionDetails()
                    {
                        IP = request.MobilexpressPayBillParameters.HttpContextParameters.UserHostAddress,
                        UserAgent = request.MobilexpressPayBillParameters.HttpContextParameters.UserAgent
                    });

                    var response = client.PayBill(dbCustomer, payableAmount, token);
                    if (response.InternalException != null)
                    {
                        //Errorslogger.Warn(response.InternalException, "Error calling 'DeleteCard' from MobilExpress client");
                        ErrorsLogger.Error(response.InternalException);
                        return new CustomerServiceMobilexpressPayBillResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, response.InternalException.Message),
                            MobilexpressPayBillResult = false,
                        };
                    }
                    if (response.Response.ResponseCode != RezaB.API.MobilExpress.Response.ResponseCodes.Success)
                    {
                        return new CustomerServiceMobilexpressPayBillResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, response.Response.ErrorMessage),
                            MobilexpressPayBillResult = false,
                        };
                    }
                    db.PayBills(dbBills, PaymentType.MobilExpress, BillPayment.AccountantType.Admin);
                    var smsService = new SMSService();
                    db.SMSArchives.AddSafely(smsService.SendSubscriberSMS(dbSubscription, SMSType.PaymentDone, new Dictionary<string, object>()
                    {
                        { SMSParamaterRepository.SMSParameterNameCollection.BillTotal, payableAmount }
                    }));
                    db.SystemLogs.Add(SystemLogProcessor.BillPayment(dbBills.Select(b => b.ID), null, dbSubscription.ID, SystemLogInterface.CustomerWebsite, request.Username, PaymentType.MobilExpress));
                    db.SaveChanges();
                    return new CustomerServiceMobilexpressPayBillResponse(passwordHash, request)
                    {


                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        MobilexpressPayBillResult = true,
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceMobilexpressPayBillResponse(passwordHash, request)
                {

                    MobilexpressPayBillResult = null,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public CustomerServiceVPOSErrorParameterNameResponse GetVPOSErrorParameterName(CustomerServiceVPOSErrorParameterNameRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceVPOSErrorParameterNameResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        VPOSErrorParameterName = string.Empty,
                    };
                }
                var parameter = VPOSManager.GetErrorMessageParameterName();
                return new CustomerServiceVPOSErrorParameterNameResponse(passwordHash, request)
                {


                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                    VPOSErrorParameterName = parameter
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceVPOSErrorParameterNameResponse(passwordHash, request)
                {

                    VPOSErrorParameterName = string.Empty,

                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public string GetKeyFragment(string username)
        {
            return KeyManager.GenerateKeyFragment(username, Properties.Settings.Default.CacheDuration);
        }

        [HttpPost]
        public CustomerServiceNameValuePair CommitmentLengthList(CustomerServiceCommitmentLengthsRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceNameValuePair(passwordHash, request)
                    {
                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                var list = new LocalizedList<CommitmentLength, RadiusR.Localization.Lists.CommitmentLength>().GetList(CultureInfo.CreateSpecificCulture(request.Culture));
                var commitmentLengths = list.Select(c => new ValueNamePair()
                {
                    Name = c.Value,
                    Value = c.Key
                }).ToArray();
                return new CustomerServiceNameValuePair(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                    ValueNamePairList = commitmentLengths
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceNameValuePair(passwordHash, request)
                {
                    ValueNamePairList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        [HttpPost]
        public TelekomInfrastructureService.CustomerServiceNameValuePair GetProvinces(TelekomInfrastructureService.CustomerServiceProvincesRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {
                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                    };
                }
                var result = AddressClient.GetProvinces();
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {
                    ValueNamePairList = result.ErrorOccured == false ? result.Data.Select(p => new TelekomInfrastructureService.ValueNamePair()
                    {
                        Value = p.Code,
                        Name = p.Name
                    }).ToArray() : Enumerable.Empty<TelekomInfrastructureService.ValueNamePair>().ToArray(),
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                //Errorslogger.Error(ex, "Error Get Provinces");
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                };
            }
        }

        [HttpPost]
        public TelekomInfrastructureService.CustomerServiceNameValuePair GetProvinceDistricts(TelekomInfrastructureService.CustomerServiceNameValuePairRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {
                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.ItemCode == null)
                {
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {

                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),

                    };
                }
                var result = AddressClient.GetProvinceDistricts(request.ItemCode.Value);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = result.ErrorOccured == false ? result.Data.Select(p => new TelekomInfrastructureService.ValueNamePair()
                    {
                        Value = p.Code,
                        Name = p.Name
                    }).ToArray() : Enumerable.Empty<TelekomInfrastructureService.ValueNamePair>().ToArray(),
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {
                    ValueNamePairList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                };
            }
        }

        [HttpPost]
        public TelekomInfrastructureService.CustomerServiceNameValuePair GetDistrictRuralRegions(TelekomInfrastructureService.CustomerServiceNameValuePairRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {
                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.ItemCode == null)
                {
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {

                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),

                    };
                }
                var result = AddressClient.GetDistrictRuralRegions(request.ItemCode.Value);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = result.ErrorOccured == false ? result.Data.Select(p => new TelekomInfrastructureService.ValueNamePair()
                    {
                        Value = p.Code,
                        Name = p.Name
                    }).ToArray() : Enumerable.Empty<TelekomInfrastructureService.ValueNamePair>().ToArray(),
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                };
            }
        }

        [HttpPost]
        public TelekomInfrastructureService.CustomerServiceNameValuePair GetRuralRegionNeighbourhoods(TelekomInfrastructureService.CustomerServiceNameValuePairRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {
                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.ItemCode == null)
                {
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {
                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),

                    };
                }
                var result = AddressClient.GetRuralRegionNeighbourhoods(request.ItemCode.Value);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = result.ErrorOccured == false ? result.Data.Select(p => new TelekomInfrastructureService.ValueNamePair()
                    {
                        Value = p.Code,
                        Name = p.Name
                    }).ToArray() : Enumerable.Empty<TelekomInfrastructureService.ValueNamePair>().ToArray(),
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                };
            }
        }

        [HttpPost]
        public TelekomInfrastructureService.CustomerServiceNameValuePair GetNeighbourhoodStreets(TelekomInfrastructureService.CustomerServiceNameValuePairRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {


                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.ItemCode == null)
                {
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {

                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),

                    };
                }
                var result = AddressClient.GetNeighbourhoodStreets(request.ItemCode.Value);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = result.ErrorOccured == false ? result.Data.Select(p => new TelekomInfrastructureService.ValueNamePair()
                    {
                        Value = p.Code,
                        Name = p.Name
                    }).ToArray() : Enumerable.Empty<TelekomInfrastructureService.ValueNamePair>().ToArray(),
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                };
            }
        }

        [HttpPost]
        public TelekomInfrastructureService.CustomerServiceNameValuePair GetStreetBuildings(TelekomInfrastructureService.CustomerServiceNameValuePairRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {
                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.ItemCode == null)
                {
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {

                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),

                    };
                }
                var result = AddressClient.GetStreetBuildings(request.ItemCode.Value);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = result.ErrorOccured == false ? result.Data.Select(p => new TelekomInfrastructureService.ValueNamePair()
                    {
                        Value = p.Code,
                        Name = p.Name
                    }).ToArray() : Enumerable.Empty<TelekomInfrastructureService.ValueNamePair>().ToArray(),
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),

                };
            }
        }

        [HttpPost]
        public TelekomInfrastructureService.CustomerServiceNameValuePair GetBuildingApartments(TelekomInfrastructureService.CustomerServiceNameValuePairRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {
                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.ItemCode == null)
                {
                    return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                    {

                        ValueNamePairList = null,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),

                    };
                }
                var result = AddressClient.GetBuildingApartments(request.ItemCode.Value);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {

                    ValueNamePairList = result.ErrorOccured == false ? result.Data.Select(p => new TelekomInfrastructureService.ValueNamePair()
                    {
                        Value = p.Code,
                        Name = p.Name
                    }).ToArray() : Enumerable.Empty<TelekomInfrastructureService.ValueNamePair>().ToArray(),
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new TelekomInfrastructureService.CustomerServiceNameValuePair(passwordHash, request)
                {
                    ValueNamePairList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                };
            }
        }

        [HttpPost]
        public TelekomInfrastructureService.CustomerServiceAddressDetailsResponse GetApartmentAddress(TelekomInfrastructureService.CustomerServiceAddressDetailsRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new TelekomInfrastructureService.CustomerServiceAddressDetailsResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        AddressDetailsResponse = null
                    };
                }
                if (request.BBK == null)
                {
                    return new TelekomInfrastructureService.CustomerServiceAddressDetailsResponse(passwordHash, request)
                    {

                        AddressDetailsResponse = null,
                        ResponseMessage = CommonResponse.NullObjectException(request.Culture),

                    };
                }
                var result = AddressClient.GetApartmentAddress(request.BBK.Value);
                return new TelekomInfrastructureService.CustomerServiceAddressDetailsResponse(passwordHash, request)
                {

                    AddressDetailsResponse = new TelekomInfrastructureService.AddressDetailsResponse()
                    {
                        AddressNo = result.Data.AddressNo,
                        AddressText = result.Data.AddressText,
                        ApartmentID = result.Data.ApartmentID,
                        ApartmentNo = result.Data.ApartmentNo,
                        DistrictID = result.Data.DistrictID,
                        DistrictName = result.Data.DistrictName,
                        DoorID = result.Data.DoorID,
                        DoorNo = result.Data.DoorNo,
                        NeighbourhoodID = result.Data.NeighbourhoodID,
                        NeighbourhoodName = result.Data.NeighbourhoodName,
                        ProvinceID = result.Data.ProvinceID,
                        ProvinceName = result.Data.ProvinceName,
                        RuralCode = result.Data.RuralCode,
                        StreetID = result.Data.StreetID,
                        StreetName = result.Data.StreetName
                    },
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),

                };
            }
            catch (Exception ex)
            {

                ErrorsLogger.Error(ex);
                return new TelekomInfrastructureService.CustomerServiceAddressDetailsResponse(passwordHash, request)
                {
                    AddressDetailsResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        [HttpPost]
        public CustomerServiceExternalTariffResponse ExternalTariffList(CustomerServiceExternalTariffRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceExternalTariffResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        ExternalTariffList = null
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var tariffs = db.ExternalTariffs.Select(t => new ExternalTariffResponse()
                    {
                        DisplayName = t.DisplayName,
                        HasFiber = t.HasFiber,
                        HasXDSL = t.HasXDSL,
                        TariffID = t.TariffID,
                        Price = t.Service.Price,
                        Speed = t.Service.RateLimit
                    }).ToArray();
                    foreach (var item in tariffs)
                    {
                        var speedRate = RezaB.Data.Formating.RateLimitParser.ParseString(item.Speed);
                        item.Speed = $"{speedRate.DownloadRate} {speedRate.DownloadRateSuffix}";
                    }
                    return new CustomerServiceExternalTariffResponse(passwordHash, request)
                    {
                        ExternalTariffList = tariffs,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceExternalTariffResponse(passwordHash, request)
                {
                    ExternalTariffList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),

                };
            }
        }

        [HttpPost]
        public TelekomInfrastructureService.CustomerServiceServiceAvailabilityResponse ServiceAvailability(TelekomInfrastructureService.CustomerServiceServiceAvailabilityRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new TelekomInfrastructureService.CustomerServiceServiceAvailabilityResponse(passwordHash, request)
                    {
                        ServiceAvailabilityResponse = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    var domain = RadiusR.DB.DomainsCache.DomainsCache.GetDomainByID(RadiusR.DB.CustomerWebsiteSettings.WebsiteServicesInfrastructureDomainID);
                    var client = new AvailabilityServiceClient(domain.TelekomCredential.XDSLWebServiceUsernameInt, domain.TelekomCredential.XDSLWebServicePassword);
                    var xdslTypeAdsl = AvailabilityServiceClient.XDSLType.ADSL;
                    var xdslTypeVdsl = AvailabilityServiceClient.XDSLType.VDSL;
                    var xdslTypeFiber = AvailabilityServiceClient.XDSLType.Fiber;
                    var queryType = AvailabilityServiceClient.QueryType.BBK;
                    List<Thread> threads = new List<Thread>();
                    RezaB.TurkTelekom.WebServices.ServiceResponse<AvailabilityServiceClient.AvailabilityDescription> availabAdsl = null, availabVdsl = null, availabFiber = null;
                    Thread threadAdsl = new Thread(() => { availabAdsl = client.Check(xdslTypeAdsl, queryType, request.ServiceAvailabilityParameters.bbk); });
                    Thread threadVdsl = new Thread(() => { availabVdsl = client.Check(xdslTypeVdsl, queryType, request.ServiceAvailabilityParameters.bbk); });
                    Thread threadFiber = new Thread(() => { availabFiber = client.Check(xdslTypeFiber, queryType, request.ServiceAvailabilityParameters.bbk); });
                    threadAdsl.Start();
                    threadVdsl.Start();
                    threadFiber.Start();
                    threads.Add(threadAdsl);
                    threads.Add(threadVdsl);
                    threads.Add(threadFiber);
                    foreach (var item in threads)
                    {
                        item.Join();
                    }
                    bool HasInfrastructureAdsl = availabAdsl.InternalException != null ? false : availabAdsl.Data.Description.ErrorMessage == null ? availabAdsl.Data.Description.HasInfrastructure.Value : false;
                    bool HasInfrastructureVdsl = availabVdsl.InternalException != null ? false : availabVdsl.Data.Description.ErrorMessage == null ? availabVdsl.Data.Description.HasInfrastructure.Value : false;
                    bool HasInfrastructureFiber = availabFiber.InternalException != null ? false : availabFiber.Data.Description.ErrorMessage == null ? availabFiber.Data.Description.HasInfrastructure.Value : false;
                    var speedAdsl = HasInfrastructureAdsl ? availabAdsl.Data.Description.DSLMaxSpeed.Value : 0;
                    var speedVdsl = HasInfrastructureVdsl ? availabVdsl.Data.Description.DSLMaxSpeed.Value : 0;
                    var speedFiber = HasInfrastructureFiber ? availabFiber.Data.Description.DSLMaxSpeed.Value : 0;
                    AddressServiceClient addressServiceClient = new AddressServiceClient(domain.TelekomCredential.XDSLWebServiceUsernameInt, domain.TelekomCredential.XDSLWebServicePassword);
                    var address = addressServiceClient.GetAddressFromCode(Convert.ToInt64(request.ServiceAvailabilityParameters.bbk));
                    return new TelekomInfrastructureService.CustomerServiceServiceAvailabilityResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        ServiceAvailabilityResponse = new TelekomInfrastructureService.ServiceAvailabilityResponse()
                        {
                            address = address.InternalException == null ? address.Data.AddressText : "-",
                            ADSL = new TelekomInfrastructureService.ServiceAvailabilityResponse.ADSLInfo()
                            {
                                HasInfrastructureAdsl = HasInfrastructureAdsl,
                                AdslDistance = availabAdsl.InternalException == null ? availabAdsl.Data.Description.Distance : null,
                                AdslPortState = availabAdsl.InternalException == null ? RadiusR.Localization.Lists.PortState.ResourceManager.GetString(availabAdsl.Data.Description.PortState.ToString(), CultureInfo.CreateSpecificCulture(request.Culture)) : RadiusR.Localization.Lists.PortState.ResourceManager.GetString(AvailabilityServiceClient.PortState.NotAvailable.ToString(), CultureInfo.CreateSpecificCulture(request.Culture)),
                                AdslSpeed = availabAdsl.InternalException == null ? availabAdsl.Data.Description.DSLMaxSpeed : null,
                                AdslSVUID = availabAdsl.InternalException == null ? availabAdsl.Data.Description.SVUID : "-",
                                PortState = availabAdsl.InternalException == null ? (int)availabAdsl.Data.Description.PortState : (int)AvailabilityServiceClient.PortState.NotAvailable
                            },
                            VDSL = new TelekomInfrastructureService.ServiceAvailabilityResponse.VDSLInfo()
                            {
                                HasInfrastructureVdsl = HasInfrastructureVdsl,
                                VdslDistance = availabVdsl.InternalException == null ? availabVdsl.Data.Description.Distance : null,
                                VdslPortState = availabVdsl.InternalException == null ? RadiusR.Localization.Lists.PortState.ResourceManager.GetString(availabVdsl.Data.Description.PortState.ToString(), CultureInfo.CreateSpecificCulture(request.Culture)) : RadiusR.Localization.Lists.PortState.ResourceManager.GetString(AvailabilityServiceClient.PortState.NotAvailable.ToString(), CultureInfo.CreateSpecificCulture(request.Culture)),
                                VdslSpeed = availabVdsl.InternalException == null ? availabVdsl.Data.Description.DSLMaxSpeed : null,
                                VdslSVUID = availabVdsl.InternalException == null ? availabVdsl.Data.Description.SVUID : "-",
                                PortState = availabAdsl.InternalException == null ? (int)availabVdsl.Data.Description.PortState : (int)AvailabilityServiceClient.PortState.NotAvailable
                            },
                            FIBER = new TelekomInfrastructureService.ServiceAvailabilityResponse.FIBERInfo()
                            {
                                HasInfrastructureFiber = HasInfrastructureFiber,
                                FiberDistance = availabFiber.InternalException == null ? availabFiber.Data.Description.Distance : null,
                                FiberPortState = availabFiber.InternalException == null ? RadiusR.Localization.Lists.PortState.ResourceManager.GetString(availabFiber.Data.Description.PortState.ToString(), CultureInfo.CreateSpecificCulture(request.Culture)) : RadiusR.Localization.Lists.PortState.ResourceManager.GetString(AvailabilityServiceClient.PortState.NotAvailable.ToString(), CultureInfo.CreateSpecificCulture(request.Culture)),
                                FiberSpeed = availabFiber.InternalException == null ? availabFiber.Data.Description.DSLMaxSpeed : null,
                                FiberSVUID = availabFiber.InternalException == null ? availabFiber.Data.Description.SVUID : "-",
                                PortState = availabAdsl.InternalException == null ? (int)availabFiber.Data.Description.PortState : (int)AvailabilityServiceClient.PortState.NotAvailable
                            },
                            BBK = request.ServiceAvailabilityParameters.bbk
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new TelekomInfrastructureService.CustomerServiceServiceAvailabilityResponse(passwordHash, request)
                {
                    ServiceAvailabilityResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        [HttpPost]
        public CustomerServiceGetCustomerFileResponse GetCustomerFiles(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceGetCustomerFileResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        CustomerFiles = null
                    };
                }
                if (request.SubscriptionParameters.SubscriptionId == null)
                {
                    return new CustomerServiceGetCustomerFileResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        CustomerFiles = null
                    };
                }
                var fileManager = new RadiusR.FileManagement.MasterISSFileManager();
                using (var files = fileManager.GetClientAttachmentsList(request.SubscriptionParameters.SubscriptionId.Value))
                {
                    if (files.InternalException != null)
                    {
                        return new CustomerServiceGetCustomerFileResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, files.InternalException.Message),
                            CustomerFiles = null
                        };
                    }
                    var currentFiles = files.Result.ToArray();
                    var customerFiles = currentFiles.Select(f => new GetCustomerFilesResponse()
                    {
                        FileInfo = new RadiusR.API.CustomerWebService.Responses.FileInfo()
                        {
                            Type = (int)f.AttachmentType,
                            Name = new LocalizedList<FileManagement.SpecialFiles.ClientAttachmentTypes, RadiusR.Localization.Lists.ClientAttachmentTypes>().GetDisplayText((int)f.AttachmentType, CultureInfo.CreateSpecificCulture(request.Culture))
                        },
                        FileExtention = f.FileExtention,
                        MIMEType = f.MIMEType,
                        ServerSideName = f.ServerSideName
                    }).ToArray();
                    return new CustomerServiceGetCustomerFileResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        CustomerFiles = customerFiles
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetCustomerFileResponse(passwordHash, request)
                {
                    CustomerFiles = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),

                };
            }
        }
        [HttpPost]
        public CustomerServiceGetClientAttachmentResponse GetClientAttachment(CustomerServiceGetClientAttachmentRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceGetClientAttachmentResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        GetClientAttachment = null
                    };
                }
                if (request.GetClientAttachment.SubscriptionId == null)
                {
                    return new CustomerServiceGetClientAttachmentResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        GetClientAttachment = null
                    };
                }
                var fileManager = new RadiusR.FileManagement.MasterISSFileManager();
                using (var clientFile = fileManager.GetClientAttachment(request.GetClientAttachment.SubscriptionId.Value, request.GetClientAttachment.FileName))
                {
                    if (clientFile.InternalException != null)
                    {
                        return new CustomerServiceGetClientAttachmentResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, clientFile.InternalException.Message),
                            GetClientAttachment = null
                        };
                    }
                    byte[] content = null;
                    using (var memoryStream = new MemoryStream())
                    {
                        clientFile.Result.Content.CopyTo(memoryStream);
                        content = memoryStream.ToArray();
                    }
                    var customerFiles = new GetClientAttachmentResponse()
                    {
                        FileInfo = new RadiusR.API.CustomerWebService.Responses.FileInfo()
                        {
                            Type = (int)clientFile.Result.FileDetail.AttachmentType,
                            Name = new LocalizedList<FileManagement.SpecialFiles.ClientAttachmentTypes, RadiusR.Localization.Lists.ClientAttachmentTypes>().GetDisplayText((int)clientFile.Result.FileDetail.AttachmentType, CultureInfo.CreateSpecificCulture(request.Culture))
                        },
                        FileExtention = clientFile.Result.FileDetail.FileExtention,
                        MIMEType = clientFile.Result.FileDetail.MIMEType,
                        ServerSideName = clientFile.Result.FileDetail.ServerSideName,
                        Content = content
                    };
                    return new CustomerServiceGetClientAttachmentResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        GetClientAttachment = customerFiles
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetClientAttachmentResponse(passwordHash, request)
                {
                    GetClientAttachment = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),

                };
            }
        }

        [HttpPost]
        public CustomerServicGetSupportAttachmentListResponse GetSupportAttachmentList(CustomerServiceGetSupportAttachmentListRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new CustomerServicGetSupportAttachmentListResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        GetSupportAttachmentList = null
                    };
                }
                if (request.GetSupportAttachmentsParameters.RequestId == null)
                {
                    return new CustomerServicGetSupportAttachmentListResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.SupportRequestNotFound(request.Culture),
                        GetSupportAttachmentList = null
                    };
                }
                var fileManager = new RadiusR.FileManagement.MasterISSFileManager();
                using (var clientFile = fileManager.GetSupportRequestAttachmentList(request.GetSupportAttachmentsParameters.RequestId.Value))
                {
                    if (clientFile.InternalException != null)
                    {
                        return new CustomerServicGetSupportAttachmentListResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, clientFile.InternalException.Message),
                            GetSupportAttachmentList = null
                        };
                    }
                    var supportFiles = clientFile.Result.Select(f => new GetSupportAttachmentListResponse()
                    {
                        FileExtention = f.FileExtention,
                        MIMEType = f.MIMEType,
                        ServerSideName = f.ServerSideName,
                        Datetime = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(f.CreationDate),
                        FileName = f.FileName,
                        MD5 = f.MD5,
                        StageId = f.StageId
                    }).ToArray();
                    return new CustomerServicGetSupportAttachmentListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        GetSupportAttachmentList = supportFiles
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServicGetSupportAttachmentListResponse(passwordHash, request)
                {
                    GetSupportAttachmentList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),

                };
            }
        }
        [HttpPost]
        public CustomerServiceGetSupportAttachmentResponse GetSupportAttachment(CustomerServiceGetSupportAttachmentRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new CustomerServiceGetSupportAttachmentResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        GetSupportAttachment = null
                    };
                }
                if (request.GetSupportAttachmentParameters.SupportRequestId == null)
                {
                    return new CustomerServiceGetSupportAttachmentResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.SupportRequestNotFound(request.Culture),
                        GetSupportAttachment = null
                    };
                }
                var fileManager = new RadiusR.FileManagement.MasterISSFileManager();
                using (var clientFile = fileManager.GetSupportRequestAttachment(request.GetSupportAttachmentParameters.SupportRequestId.Value, request.GetSupportAttachmentParameters.FileName))
                {
                    if (clientFile.InternalException != null)
                    {
                        return new CustomerServiceGetSupportAttachmentResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, clientFile.InternalException.Message),
                            GetSupportAttachment = null
                        };
                    }
                    byte[] content = null;
                    using (var memoryStream = new MemoryStream())
                    {
                        clientFile.Result.Content.CopyTo(memoryStream);
                        content = memoryStream.ToArray();
                    }
                    var supportFile = new GetSupportAttachmentResponse()
                    {
                        CreationDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(clientFile.Result.FileDetail.CreationDate),
                        FileExtention = clientFile.Result.FileDetail.FileExtention,
                        FileName = clientFile.Result.FileDetail.FileName,
                        MD5 = clientFile.Result.FileDetail.MD5,
                        MIMEType = clientFile.Result.FileDetail.MIMEType,
                        ServerSideName = clientFile.Result.FileDetail.ServerSideName,
                        StageId = clientFile.Result.FileDetail.StageId,
                        FileContent = content
                    };
                    return new CustomerServiceGetSupportAttachmentResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        GetSupportAttachment = supportFile
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetSupportAttachmentResponse(passwordHash, request)
                {
                    GetSupportAttachment = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),

                };
            }
        }

        //[HttpPost] public CustomerServiceSaveSupportAttachmentResponse SaveSupportAttachment(CustomerServiceSaveSupportAttachmentRequest request)
        //{
        //    var password = new ServiceSettings().GetUserPassword(request.Username);
        //    var passwordHash = HashUtilities.GetHexString<SHA1>(password);
        //    try
        //    {
        //        CustomerInComingInfo.Trace(request);
        //        if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
        //        {
        //            return new CustomerServiceSaveSupportAttachmentResponse(password, request)
        //            {
        //                ResponseMessage = CommonResponse.UnauthorizedResponse(request),
        //                SaveSupportAttachmentResult = false
        //            };
        //        }
        //        if (request.SaveSupportAttachmentParameters.StageId == null || request.SaveSupportAttachmentParameters.SupportRequestId == null)
        //        {
        //            return new CustomerServiceSaveSupportAttachmentResponse(passwordHash, request)
        //            {
        //                ResponseMessage = CommonResponse.SupportRequestNotFound(request.Culture),
        //                SaveSupportAttachmentResult = false
        //            };
        //        }
        //        var fileManager = new RadiusR.FileManagement.MasterISSFileManager();
        //        var fileStream = new MemoryStream(request.SaveSupportAttachmentParameters.FileContent);
        //        var saveAttachment = fileManager.SaveSupportRequestAttachment(
        //            request.SaveSupportAttachmentParameters.SupportRequestId.Value,
        //            new FileManagement.SpecialFiles.FileManagerSupportRequestAttachmentWithContent(
        //                fileStream,
        //                new FileManagement.SpecialFiles.FileManagerSupportRequestAttachment(
        //                    request.SaveSupportAttachmentParameters.StageId.Value,
        //                    request.SaveSupportAttachmentParameters.FileName.Split('.')[0],
        //                    request.SaveSupportAttachmentParameters.FileExtention.Replace(".", ""))));
        //        if (saveAttachment.InternalException != null)
        //        {
        //            return new CustomerServiceSaveSupportAttachmentResponse(passwordHash, request)
        //            {
        //                SaveSupportAttachmentResult = false,
        //                ResponseMessage = CommonResponse.FailedResponse(request.Culture, saveAttachment.InternalException.Message)
        //            };
        //        }
        //        return new CustomerServiceSaveSupportAttachmentResponse(passwordHash, request)
        //        {
        //            SaveSupportAttachmentResult = saveAttachment.Result,
        //            ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        ErrorsLogger.Error(ex);
        //        return new CustomerServiceSaveSupportAttachmentResponse(passwordHash, request)
        //        {
        //            SaveSupportAttachmentResult = false,
        //            ResponseMessage = CommonResponse.InternalException(request.Culture, ex),

        //        };
        //    }
        //}
        [HttpPost]
        public CustomerServiceCustomerAuthenticationWithPasswordResponse CustomerAuthenticationWithPassword(CustomerServiceAuthenticationWithPasswordRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new CustomerServiceCustomerAuthenticationWithPasswordResponse(passwordHash, request)
                    {
                        AuthenticationWithPasswordResult = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.AuthenticationWithPasswordParameters.CustomerCode.StartsWith("0"))
                    request.AuthenticationWithPasswordParameters.CustomerCode = request.AuthenticationWithPasswordParameters.CustomerCode.TrimStart('0');
                using (RadiusREntities db = new RadiusREntities())
                {
                    // find customers
                    var dbCustomers = db.Customers.Where(c => c.CustomerIDCard.TCKNo == request.AuthenticationWithPasswordParameters.CustomerCode || c.ContactPhoneNo == request.AuthenticationWithPasswordParameters.CustomerCode).ToArray();
                    // select a subscriber
                    var dbClient = dbCustomers.SelectMany(c => c.Subscriptions).FirstOrDefault();
                    var selectedDbClient = dbCustomers.SelectMany(c => c.Subscriptions).Where(s => s.State == (short)CustomerState.Active).FirstOrDefault() ?? dbClient;
                    if (dbCustomers.Count() > 0 && dbClient != null)
                    {
                        // if need to send a new password
                        if (string.IsNullOrEmpty(dbClient.OnlinePassword) || !dbClient.OnlinePasswordExpirationDate.HasValue)
                        {
                            var dbClients = db.Subscriptions.Where(sub => (sub.Customer.CustomerIDCard.TCKNo == request.AuthenticationWithPasswordParameters.CustomerCode) || sub.Customer.ContactPhoneNo == request.AuthenticationWithPasswordParameters.CustomerCode).ToList();
                            var PassiveSubscriptions = dbClients.Where(s => s.State == (short)CustomerState.Cancelled && !s.Bills.Where(b => b.BillStatusID == (short)BillState.Unpaid).Any()).ToList();
                            if (PassiveSubscriptions.Count() == dbClients.Count())
                            {
                                ErrorsLogger.Error(new Exception($"CustomerAuthentication -> cancelled . CustomerCode : {request.AuthenticationWithPasswordParameters.CustomerCode} ."));
                                //Errorslogger.Error($"CustomerAuthentication -> cancelled . CustomerCode : {request.AuthenticationWithPasswordParameters.CustomerCode} . User : {request.Username}");
                                return new CustomerServiceCustomerAuthenticationWithPasswordResponse(passwordHash, request)
                                {
                                    AuthenticationWithPasswordResult = null,
                                    ResponseMessage = CommonResponse.ClientNotFound(request.Culture),
                                };
                            }
                            var randomPassword = new Random().Next(100000, 1000000).ToString("000000");
                            dbClients.ForEach(client => client.OnlinePassword = randomPassword);
                            dbClients.ForEach(client => client.OnlinePasswordExpirationDate = DateTime.Now.Add(CustomerWebsiteSettings.OnlinePasswordDuration));
                            db.SaveChanges();
                        }
                        if (dbClient.OnlinePassword != request.AuthenticationWithPasswordParameters.Password)
                        {
                            return new CustomerServiceCustomerAuthenticationWithPasswordResponse(passwordHash, request)
                            {
                                AuthenticationWithPasswordResult = null,
                                ResponseMessage = CommonResponse.ClientNotFound(request.Culture)
                            };
                        }
                        // return success
                        var relatedCustomers = GetRelatedCustomers(dbCustomers); //dbCustomers.SelectMany(c => c.Subscriptions).Select(s => s.ID + "," + s.SubscriberNo).ToArray();
                        return new CustomerServiceCustomerAuthenticationWithPasswordResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                            AuthenticationWithPasswordResult = new CustomerAuthenticationWithPasswordResponse()
                            {
                                ID = selectedDbClient.ID,
                                SubscriberNo = selectedDbClient.SubscriberNo,
                                ValidDisplayName = selectedDbClient.ValidDisplayName,
                                RelatedCustomers = relatedCustomers
                            }
                        };
                    }
                    else
                    {
                        return new CustomerServiceCustomerAuthenticationWithPasswordResponse(passwordHash, request)
                        {
                            AuthenticationWithPasswordResult = null,
                            ResponseMessage = CommonResponse.ClientNotFound(request.Culture)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceCustomerAuthenticationWithPasswordResponse(passwordHash, request)
                {
                    AuthenticationWithPasswordResult = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }

        [HttpPost]
        public CustomerServiceHasClientPreRegisterResponse HasClientPreRegisterSubscription(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new CustomerServiceHasClientPreRegisterResponse(passwordHash, request)
                    {
                        HasClientPreRegister = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (request.SubscriptionParameters.SubscriptionId == null)
                {
                    return new CustomerServiceHasClientPreRegisterResponse(passwordHash, request)
                    {
                        HasClientPreRegister = null,
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture)
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbClient = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId);
                    if (dbClient == null)
                    {
                        return new CustomerServiceHasClientPreRegisterResponse(passwordHash, request)
                        {
                            HasClientPreRegister = null,
                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture)
                        };
                    }
                    var IsCurrentPreRegister = dbClient.State == (short)CustomerState.PreRegisterd;
                    var hasClientPreRegister = dbClient.Customer.Subscriptions.Where(client => client.State == (short)RadiusR.DB.Enums.CustomerState.PreRegisterd).FirstOrDefault() != null;
                    return new CustomerServiceHasClientPreRegisterResponse(passwordHash, request)
                    {
                        HasClientPreRegister = new HasPreRegisterResponse()
                        {
                            HasPreRegister = hasClientPreRegister,
                            IsCurrentPreRegister = IsCurrentPreRegister
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceHasClientPreRegisterResponse(passwordHash, request)
                {
                    HasClientPreRegister = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }

        [HttpPost]
        public CustomerServiceSaveClientAttachmentResponse SaveClientAttachment(CustomerServiceSaveClientAttachmentRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new CustomerServiceSaveClientAttachmentResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        SaveClientAttachmentResult = false
                    };
                }
                if (request.SaveClientAttachmentParameters == null || request.SaveClientAttachmentParameters.SubscriptionId == null)
                {
                    return new CustomerServiceSaveClientAttachmentResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
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
                    return new CustomerServiceSaveClientAttachmentResponse(passwordHash, request)
                    {
                        SaveClientAttachmentResult = false,
                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, saveAttachment.InternalException.Message)
                    };
                }
                return new CustomerServiceSaveClientAttachmentResponse(passwordHash, request)
                {
                    SaveClientAttachmentResult = saveAttachment.Result,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceSaveClientAttachmentResponse(passwordHash, request)
                {
                    SaveClientAttachmentResult = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),

                };
            }
        }
        [HttpPost]
        public CustomerServiceGetClientPDFFormResponse GetClientPDFForm(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new CustomerServiceGetClientPDFFormResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        GetClientPDFFormResult = null
                    };
                }
                if (request.SubscriptionParameters == null || request.SubscriptionParameters.SubscriptionId == null)
                {
                    return new CustomerServiceGetClientPDFFormResponse(passwordHash, request)
                    {
                        GetClientPDFFormResult = null,
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture)
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var subscription = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId);
                    var createdPDF = RadiusR.PDFForms.PDFWriter.GetContractPDF(db, request.SubscriptionParameters.SubscriptionId.Value, CultureInfo.CreateSpecificCulture(request.Culture));
                    if (createdPDF.InternalException != null)
                    {
                        ErrorsLogger.Error(new Exception($" pdf create is failed. exception is : {createdPDF.InternalException}"));
                        //Errorslogger.Error($" pdf create is failed. exception is : {createdPDF.InternalException}");
                        return new CustomerServiceGetClientPDFFormResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                            GetClientPDFFormResult = null,
                        };
                    }
                    byte[] content = null;
                    using (var memoryStream = new MemoryStream())
                    {
                        createdPDF.Result.CopyTo(memoryStream);
                        content = memoryStream.ToArray();
                    }
                    return new CustomerServiceGetClientPDFFormResponse(passwordHash, request)
                    {
                        GetClientPDFFormResult = new GetClientPDFFormResponse()
                        {
                            FileContent = content,
                            FileName = string.Format(RadiusR.Localization.Pages.Common.ContractFileName, subscription.SubscriberNo),
                            MIMEType = "application/pdf"
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceGetClientPDFFormResponse(passwordHash, request)
                {
                    GetClientPDFFormResult = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),

                };
            }
        }
        [HttpPost]
        public CustomerServiceEArchivePDFMailResponse EArchivePDFMail(CustomerServiceEArchivePDFRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                    {
                        ErrorsLogger.Error(new Exception("unauthorize error."));
                        //Errorslogger.Error($"EArchivePDF unauthorize error. User : {request.Username}");
                        return new CustomerServiceEArchivePDFMailResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                            EArchivePDFMailResponse = null,
                        };
                    }
                    var dbBill = db.Bills.Find(request.EArchivePDFParameters.BillId);
                    if (dbBill == null || dbBill.EBill == null || dbBill.EBill.EBillType != (short)EBillType.EArchive)
                    {
                        ErrorsLogger.Error(new Exception($"EArchivePDF -> Bill not found. Bill Id : {request.EArchivePDFParameters.BillId}."));
                        //Errorslogger.Error($"EArchivePDF -> Bill not found. Bill Id : {request.EArchivePDFParameters.BillId} . User : {request.Username}");
                        return new CustomerServiceEArchivePDFMailResponse(passwordHash, request)
                        {
                            EArchivePDFMailResponse = null,
                            ResponseMessage = CommonResponse.BillsNotFoundException(request.Culture)
                        };
                    }
                    if (dbBill.Subscription.ID != request.EArchivePDFParameters.SubscriptionId)
                    {
                        ErrorsLogger.Error(new Exception($"EArchivePDF -> Bill id and subscription id not match. Bill Id : {request.EArchivePDFParameters.BillId} - Subscription Id : {request.EArchivePDFParameters.SubscriptionId}."));
                        //Errorslogger.Error($"EArchivePDF -> Bill id and subscription id not match. Bill Id : {request.EArchivePDFParameters.BillId} - Subscription Id : {request.EArchivePDFParameters.SubscriptionId}. User : {request.Username}");
                        return new CustomerServiceEArchivePDFMailResponse(passwordHash, request)
                        {
                            EArchivePDFMailResponse = null,
                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        };
                    }
                    var serviceClient = new RezaB.NetInvoice.Wrapper.NetInvoiceClient(AppSettings.EBillCompanyCode, AppSettings.EBillApiUsername, AppSettings.EBillApiPassword);
                    var response = serviceClient.GetEArchivePDF(dbBill.EBill.ReferenceNo);
                    if (response.PDFData == null)
                    {
                        return new CustomerServiceEArchivePDFMailResponse(passwordHash, request)
                        {
                            EArchivePDFMailResponse = null,
                            ResponseMessage = CommonResponse.EArchivePDFNotFound(request.Culture)
                        };
                    }
                    if (string.IsNullOrEmpty(dbBill.Subscription.Customer.Email))
                    {
                        return new CustomerServiceEArchivePDFMailResponse(passwordHash, request)
                        {
                            EArchivePDFMailResponse = null,
                            ResponseMessage = CommonResponse.CustomerMailNotFound(request.Culture)
                        };
                    }
                    var customerCulture = string.IsNullOrEmpty(dbBill.Subscription.Customer.Culture) ? "tr-tr" : dbBill.Subscription.Customer.Culture;
                    var fileStream = new MemoryStream(response.PDFData);
                    List<RezaB.Mailing.MailFileAttachment> attachments = new List<RezaB.Mailing.MailFileAttachment>();
                    attachments.Add(new RezaB.Mailing.MailFileAttachment()
                    {
                        Content = fileStream,
                        ContentType = "application/pdf",
                        FileName = Localization.Common.ResourceManager.GetString("EArchivePDFFileName", CultureInfo.CreateSpecificCulture(customerCulture)) + "_" + dbBill.IssueDate.ToString("yyyy-MM-dd") + ".pdf"
                    });
                    RezaB.Mailing.Client.MailClient client = new RezaB.Mailing.Client.MailClient(EmailSettings.SMTPEmailHost, EmailSettings.SMTPEMailPort, false, EmailSettings.SMTPEmailAddress, EmailSettings.SMTPEmailPassword);
                    client.SendMail(new RezaB.Mailing.StandardMailMessage(new System.Net.Mail.MailAddress(EmailSettings.SMTPEmailDisplayEmail, EmailSettings.SMTPEmailDisplayName),
                        new string[] { dbBill.Subscription.Customer.Email },
                        null,
                        null,
                        string.Format(Localization.Common.ResourceManager.GetString("EArchiveMailSubject", CultureInfo.CreateSpecificCulture(customerCulture)), dbBill.IssueDate.ToString("dd-MM-yyyy")),
                        string.Format(Localization.Common.ResourceManager.GetString("EArchiveMailBody", CultureInfo.CreateSpecificCulture(customerCulture)), dbBill.IssueDate.ToString("dd-MM-yyyy")),
                        RezaB.Mailing.MailBodyType.Text,
                        attachments));
                    return new CustomerServiceEArchivePDFMailResponse(passwordHash, request)
                    {
                        EArchivePDFMailResponse = true,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceEArchivePDFMailResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.NullObjectException(request.Culture),
                    EArchivePDFMailResponse = null,
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceEArchivePDFMailResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    EArchivePDFMailResponse = null,
                };
            }
        }
        //[HttpPost] public CustomerServiceChangeClientInfoResponse ChangeClientInfoSMSCheck(CustomerServiceChangeClientInfoRequest request)
        //{
        //    var password = new ServiceSettings().GetUserPassword(request.Username);
        //    var passwordHash = HashUtilities.GetHexString<SHA1>(password);
        //    try
        //    {
        //        CustomerInComingInfo.Trace(request);
        //        if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
        //        {
        //            return new CustomerServiceChangeClientInfoResponse(passwordHash, request)
        //            {
        //                ResponseMessage = CommonResponse.UnauthorizedResponse(request),
        //                ChangeClientInfoResponse = null
        //            };
        //        }
        //        if (string.IsNullOrEmpty(request.ChangeClientInfoRequest.ContactPhoneNo))
        //        {
        //            return new CustomerServiceChangeClientInfoResponse(passwordHash, request)
        //            {
        //                ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.ErrorMessages.ContactPhoneNoNotFound),
        //                ChangeClientInfoResponse = null
        //            };
        //        }
        //        if (request.ChangeClientInfoRequest.ContactPhoneNo.StartsWith("0"))
        //            request.ChangeClientInfoRequest.ContactPhoneNo = request.ChangeClientInfoRequest.ContactPhoneNo.TrimStart('0');
        //        var rand = new Random();
        //        var smsCode = rand.Next(100000, 1000000).ToString();
        //        SMSService SMS = new SMSService();
        //        SMS.SendGenericSMS(request.ChangeClientInfoRequest.ContactPhoneNo, request.Culture, rawText: string.Format(Localization.Common.OperationSMS, smsCode, CustomerWebsiteSettings.OnlinePasswordDuration.Hours));
        //        Errorslogger.LogInfo(request.Username, $"Change Client Info SMS Code : {smsCode}");
        //        return new CustomerServiceChangeClientInfoResponse(passwordHash, request)
        //        {
        //            ChangeClientInfoResponse = new ChangeClientInfoResponse()
        //            {
        //                SMSCode = smsCode
        //            },
        //            ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        ErrorsLogger.Error(ex);
        //        return new CustomerServiceChangeClientInfoResponse(passwordHash, request)
        //        {
        //            ResponseMessage = CommonResponse.InternalException(request.Culture),
        //            ChangeClientInfoResponse = null,
        //        };
        //    }
        //}
        //[HttpPost] public CustomerServiceChangeClientInfoConfirmResponse ChangeClientInfoConfirm(CustomerServiceChangeClientInfoConfirmRequest request)
        //{
        //    var password = new ServiceSettings().GetUserPassword(request.Username);
        //    var passwordHash = HashUtilities.GetHexString<SHA1>(password);
        //    try
        //    {
        //        CustomerInComingInfo.Trace(request);
        //        if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
        //        {
        //            return new CustomerServiceChangeClientInfoConfirmResponse(passwordHash, request)
        //            {
        //                ResponseMessage = CommonResponse.UnauthorizedResponse(request),
        //                ChangeClientInfoConfirmResult = null
        //            };
        //        }
        //        if (request.ChangeClientInfoConfirmRequest.SubscriptionId == null)
        //        {
        //            return new CustomerServiceChangeClientInfoConfirmResponse(password, request)
        //            {
        //                ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
        //                ChangeClientInfoConfirmResult = null
        //            };
        //        }
        //        if (string.IsNullOrEmpty(request.ChangeClientInfoConfirmRequest.ContactPhoneNo))
        //        {
        //            return new CustomerServiceChangeClientInfoConfirmResponse(passwordHash, request)
        //            {
        //                ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.ErrorMessages.ContactPhoneNoNotFound),
        //                ChangeClientInfoConfirmResult = null
        //            };
        //        }
        //        if (request.ChangeClientInfoConfirmRequest.ContactPhoneNo.StartsWith("0"))
        //            request.ChangeClientInfoConfirmRequest.ContactPhoneNo = request.ChangeClientInfoConfirmRequest.ContactPhoneNo.TrimStart('0');
        //        using (var db = new RadiusREntities())
        //        {
        //            var subscription = db.Subscriptions.Find(request.ChangeClientInfoConfirmRequest.SubscriptionId);
        //            if (subscription == null)
        //            {
        //                return new CustomerServiceChangeClientInfoConfirmResponse(password, request)
        //                {
        //                    ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
        //                    ChangeClientInfoConfirmResult = null
        //                };
        //            }
        //            subscription.Customer.Email = request.ChangeClientInfoConfirmRequest.Email;
        //            subscription.Customer.ContactPhoneNo = request.ChangeClientInfoConfirmRequest.ContactPhoneNo;
        //            db.SystemLogs.Add(SystemLogProcessor.ChangeCustomer(null, subscription.CustomerID, SystemLogInterface.CustomerWebsite, subscription.SubscriberNo));
        //            db.SaveChanges();
        //            return new CustomerServiceChangeClientInfoConfirmResponse(passwordHash, request)
        //            {
        //                ChangeClientInfoConfirmResult = true,
        //                ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
        //            };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        ErrorsLogger.Error(ex);
        //        return new CustomerServiceChangeClientInfoConfirmResponse(passwordHash, request)
        //        {
        //            ResponseMessage = CommonResponse.InternalException(request.Culture),
        //            ChangeClientInfoConfirmResult = null,
        //        };
        //    }
        //}
        [HttpPost]
        public CustomerServiceChangeClientInfoConfirmResponse ChangeClientOnlinePassword(CustomerServiceChangeClientOnlinePasswordRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new CustomerServiceChangeClientInfoConfirmResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        ChangeClientInfoConfirmResult = null
                    };
                }
                if (request.ChangeClientOnlinePasswordParameters.SubscriptionId == null)
                {
                    return new CustomerServiceChangeClientInfoConfirmResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        ChangeClientInfoConfirmResult = null
                    };
                }
                if (string.IsNullOrEmpty(request.ChangeClientOnlinePasswordParameters.OnlinePassword))
                {
                    return new CustomerServiceChangeClientInfoConfirmResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.ErrorMessages.OnlinePasswordCantBeNull),
                        ChangeClientInfoConfirmResult = null
                    };
                }
                if (request.ChangeClientOnlinePasswordParameters.OnlinePassword.Count() != 6)
                {
                    return new CustomerServiceChangeClientInfoConfirmResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, Localization.ErrorMessages.InvalidOnlinePassword),
                        ChangeClientInfoConfirmResult = null
                    };
                }
                using (var db = new RadiusREntities())
                {
                    //dbClients.ForEach(client => client.OnlinePassword = randomPassword);
                    var dbClient = db.Subscriptions.Find(request.ChangeClientOnlinePasswordParameters.SubscriptionId);
                    var dbClients = dbClient.Customer.Subscriptions.ToList();
                    dbClients.ForEach(client => client.OnlinePassword = request.ChangeClientOnlinePasswordParameters.OnlinePassword);
                    db.SystemLogs.Add(SystemLogProcessor.ChangeCustomer(null, dbClient.CustomerID, SystemLogInterface.CustomerWebsite, request.Username));
                    db.SaveChanges();
                    return new CustomerServiceChangeClientInfoConfirmResponse(passwordHash, request)
                    {
                        ChangeClientInfoConfirmResult = true,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceChangeClientInfoConfirmResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    ChangeClientInfoConfirmResult = null,
                };
            }
        }
        [HttpPost]
        public CustomerServiceSubscriberListResponse GetSubscriptionList(CustomerServiceBaseRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new CustomerServiceSubscriberListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request),
                        SubscriptionList = null
                    };
                }
                if (request.SubscriptionParameters.SubscriptionId == null)
                {
                    return new CustomerServiceSubscriberListResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                        SubscriptionList = null
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var customer = db.Subscriptions.Find(request.SubscriptionParameters.SubscriptionId).Customer;
                    var subscriptions = customer.Subscriptions.Select(s => new SubscriptionKeyValue()
                    {
                        SubscriptionId = s.ID,
                        State = s.State,
                        StateName = new LocalizedList<RadiusR.DB.Enums.CustomerState, RadiusR.Localization.Lists.CustomerState>().GetDisplayText(s.State, CultureInfo.CreateSpecificCulture(request.Culture))
                    }).ToArray();
                    return new CustomerServiceSubscriberListResponse(passwordHash, request)
                    {
                        SubscriptionList = subscriptions,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                return new CustomerServiceSubscriberListResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    SubscriptionList = null,
                };
            }
        }
        [HttpPost]
        public CustomerServiceAppLogResponse AppLog(CustomerServiceAppLogRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceAppLogResponse(passwordHash, request)
                    {
                        AppLogResult = false,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                if (!string.IsNullOrEmpty(request.LogDescription))
                {
                    MobileLogger.Error(new Exception(request.LogDescription));
                }
                return new CustomerServiceAppLogResponse(passwordHash, request)
                {
                    AppLogResult = true,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                //Errorslogger.Error(ex, "Error Get new customer register");
                return new CustomerServiceAppLogResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    AppLogResult = false
                };
            }
        }
        [HttpPost]
        public CustomerServiceExistingCustomerRegisterResponse ExistingCustomerRegister(CustomerServiceExistingCustomerRegisterRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.Trace(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    ErrorsLogger.Error(new Exception("unauthorize error"));
                    return new CustomerServiceExistingCustomerRegisterResponse(passwordHash, request)
                    {
                        KeyValuePairs = null,
                        ResponseMessage = CommonResponse.UnauthorizedResponse(request)
                    };
                }
                using (var db = new RadiusR.DB.RadiusREntities())
                {
                    var referenceCustomer = db.Subscriptions.Find(request.ExistingCustomerRegister.SubscriberID);
                    if (referenceCustomer == null)
                    {
                        return new CustomerServiceExistingCustomerRegisterResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.SubscriberNotFoundErrorResponse(request.Culture),
                            KeyValuePairs = null
                        };
                    }
                    var specialOfferId = db.SpecialOffers.Where(s => s.IsReferral == true && DateTime.Now > s.StartDate && DateTime.Now < s.EndDate).ToList();
                    if (request.ExistingCustomerRegister.RegistrationInfo.ReferralDiscount != null)
                    {
                        if (specialOfferId.Count != 1)
                        {
                            return new CustomerServiceExistingCustomerRegisterResponse(passwordHash, request)
                            {
                                KeyValuePairs = null,
                                ResponseMessage = CommonResponse.SpecialOfferError(request.Culture)
                            };
                        }
                    }
                    var externalTariff = db.ExternalTariffs.GetActiveExternalTariffs().FirstOrDefault(ext => ext.TariffID == request.ExistingCustomerRegister.RegistrationInfo.ServiceID);
                    if (externalTariff == null)
                    {
                        return new CustomerServiceExistingCustomerRegisterResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.TariffNotFound(request.Culture),
                            KeyValuePairs = null
                        };
                    }
                    var billingPeriod = externalTariff.Service.GetBestBillingPeriod(DateTime.Now.Day);
                    var AddressInfo = request.ExistingCustomerRegister.RegistrationInfo.SetupAddress;
                    var ReferralDiscount = request.ExistingCustomerRegister.RegistrationInfo.ReferralDiscount;
                    var registrationInfo = new CustomerRegistrationInfo.SubscriptionRegistrationInfo()
                    {
                        RegistrationType = (SubscriptionRegistrationType)request.ExistingCustomerRegister.ExtraInfo.ApplicationType,
                        TransitionPSTN = request.ExistingCustomerRegister.ExtraInfo.PSTN,
                        TransitionXDSLNo = request.ExistingCustomerRegister.ExtraInfo.XDSLNo,
                        GroupIds = RadiusR.DB.CustomerWebsiteSettings.CustomerWebsiteRegistrationGroupID == null ? null : new int[] { RadiusR.DB.CustomerWebsiteSettings.CustomerWebsiteRegistrationGroupID.Value },
                        BillingPeriod = billingPeriod,
                        CommitmentInfo = null,
                        DomainID = externalTariff.DomainID,
                        ServiceID = externalTariff.TariffID,
                        SetupAddress = new CustomerRegistrationInfo.AddressInfo()
                        {
                            AddressNo = AddressInfo.AddressNo,
                            AddressText = AddressInfo.AddressText,
                            ApartmentID = AddressInfo.ApartmentID,
                            ApartmentNo = AddressInfo.ApartmentNo,
                            DistrictID = AddressInfo.DistrictID,
                            DistrictName = AddressInfo.DistrictName,
                            DoorID = AddressInfo.DoorID,
                            DoorNo = AddressInfo.DoorNo,
                            Floor = AddressInfo.Floor,
                            NeighbourhoodID = AddressInfo.NeighbourhoodID,
                            NeighbourhoodName = AddressInfo.NeighbourhoodName,
                            PostalCode = AddressInfo.PostalCode,
                            ProvinceID = AddressInfo.ProvinceID,
                            ProvinceName = AddressInfo.ProvinceName,
                            RuralCode = AddressInfo.RuralCode,
                            StreetID = AddressInfo.StreetID,
                            StreetName = AddressInfo.StreetName,
                        },
                        ReferralDiscount = ReferralDiscount == null ? null : new CustomerRegistrationInfo.ReferralDiscountInfo()
                        {
                            ReferenceNo = ReferralDiscount.ReferenceNo,
                            SpecialOfferID = request.ExistingCustomerRegister.RegistrationInfo.ReferralDiscount != null ? specialOfferId.FirstOrDefault().ID : null
                        }
                    };
                    var result = RadiusR.DB.Utilities.ComplexOperations.Subscriptions.Registration.Registration.RegisterSubscriptionForExistingCustomer(db, registrationInfo, referenceCustomer.Customer);
                    Dictionary<string, string> valuePairs = new Dictionary<string, string>();
                    if (!result.IsSuccess)
                    {
                        var dic = result.ValidationMessages.ToDictionary(x => x.Key, x => x.ToArray());
                        foreach (var item in dic)
                        {
                            valuePairs.Add(item.Key, string.Join("-", item.Value.Select(m => !string.IsNullOrEmpty(m))));
                        }
                        return new CustomerServiceExistingCustomerRegisterResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                            KeyValuePairs = valuePairs
                        };
                    }
                    db.SaveChanges();
                    db.SystemLogs.Add(RadiusR.SystemLogs.SystemLogProcessor.AddSubscription(null, referenceCustomer.ID, referenceCustomer.Customer.ID, (SubscriptionRegistrationType)request.ExistingCustomerRegister.ExtraInfo.ApplicationType, SystemLogInterface.CustomerWebsite, $"{request.Username} ({referenceCustomer.SubscriberNo})", referenceCustomer.SubscriberNo));
                    db.SaveChanges();
                    return new CustomerServiceExistingCustomerRegisterResponse(passwordHash, request)
                    {
                        KeyValuePairs = valuePairs,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (NullReferenceException ex)
            {
                ErrorsLogger.Error(ex);
                //Errorslogger.Error(ex, "Error Null Reference Exception");
                return new CustomerServiceExistingCustomerRegisterResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.NullObjectException(request.Culture),
                    KeyValuePairs = null
                };
            }
            catch (Exception ex)
            {
                ErrorsLogger.Error(ex);
                //Errorslogger.Error(ex, "Error Get new customer register");
                return new CustomerServiceExistingCustomerRegisterResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    KeyValuePairs = null
                };
            }
        }
        #region private methods
        private string[] GetRelatedCustomers(Customer[] customers)
        {
            var subscriptions = customers.SelectMany(c => c.Subscriptions).ToList();
            var relatedCustomers = new List<string>();
            //.Select(s => s.ID + "," + s.SubscriberNo).ToArray();
            foreach (var item in subscriptions)
            {
                if ((item.State != (short)CustomerState.Dismissed))
                {
                    if (item.State == (short)CustomerState.Cancelled)
                    {
                        if (item.Bills.Where(b => b.BillStatusID == (short)BillState.Unpaid).Any())
                        {
                            relatedCustomers.Add($"{item.ID},{item.SubscriberNo}");
                        }
                    }
                    else
                    {
                        relatedCustomers.Add($"{item.ID},{item.SubscriberNo}");
                    }
                }
            }
            return relatedCustomers.ToArray();
        }
        private bool SaveSupportAttachments(Attachment[] attachments, long stageId, long supportId)
        {
            try
            {
                if (attachments != null && attachments.Length > 0)
                {
                    var SuccessFiles = new List<string>();
                    foreach (var item in attachments)
                    {
                        var fileManager = new RadiusR.FileManagement.MasterISSFileManager();
                        var fileStream = new MemoryStream(item.FileContent);
                        var saveAttachment = fileManager.SaveSupportRequestAttachment(
                            supportId,
                            new FileManagement.SpecialFiles.FileManagerSupportRequestAttachmentWithContent(
                                fileStream,
                                new FileManagement.SpecialFiles.FileManagerSupportRequestAttachment(
                                    stageId,
                                    item.FileName.Split('.')[0],
                                    item.FileExtention.Replace(".", ""))));
                        if (saveAttachment.InternalException != null)
                        {
                            foreach (var fileName in SuccessFiles)
                            {
                                fileManager.RemoveSupportRequestAttachment(supportId, fileName);
                            }
                            return false;
                        }
                        if (saveAttachment.Result)
                        {
                            SuccessFiles.Add(item.FileName.Split('.')[0]);
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        #endregion

    }
}
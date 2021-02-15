using RadiusR.API.CustomerWebService.Requests;
using RadiusR.API.CustomerWebService.Responses;
using RadiusR.DB;
using RadiusR.DB.Enums;
using RadiusR.DB.Utilities.ComplexOperations.Subscriptions.Registration;
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
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "NetspeedCustomerService" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select NetspeedCustomerService.svc or NetspeedCustomerService.svc.cs at the Solution Explorer and start debugging.
    [ServiceBehavior(AddressFilterMode = AddressFilterMode.Any)]
    public class NetspeedCustomerService : GenericCustomerService, INetspeedCustomerService, IGenericCustomerService
    {
        WebServiceLogger Errorslogger = new WebServiceLogger("Errors");
        WebServiceLogger CustomerInComingInfo = new WebServiceLogger("CustomerInComingInfo");
        public CustomerServiceExistingCustomerRegisterResponse ExistingCustomerRegister(CustomerServiceExistingCustomerRegisterRequest request)
        {
            var password = new ServiceSettings().GetUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA1>(password);
            try
            {
                CustomerInComingInfo.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    Errorslogger.LogException(request.Username, new Exception("unauthorize error"));
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
                    if (result != null)
                    {
                        var dic = result.ToDictionary(x => x.Key, x => x.ToArray());
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
                    db.SystemLogs.Add(RadiusR.SystemLogs.SystemLogProcessor.AddSubscription(null, referenceCustomer.ID, referenceCustomer.Customer.ID, SystemLogInterface.CustomerWebsite, $"{request.Username} ({referenceCustomer.SubscriberNo})", referenceCustomer.SubscriberNo));
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
                Errorslogger.LogException(request.Username, ex);
                //Errorslogger.Error(ex, "Error Null Reference Exception");
                return new CustomerServiceExistingCustomerRegisterResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.NullObjectException(request.Culture),
                    KeyValuePairs = null
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                //Errorslogger.Error(ex, "Error Get new customer register");
                return new CustomerServiceExistingCustomerRegisterResponse(passwordHash, request)
                {
                    ResponseMessage = CommonResponse.InternalException(request.Culture),
                    KeyValuePairs = null
                };
            }
        }

    }
}

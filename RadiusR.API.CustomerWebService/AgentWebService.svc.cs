using RadiusR.API.CustomerWebService.Requests.AgentRequests;
using RadiusR.API.CustomerWebService.Responses.AgentResponses;
using RadiusR.DB;
using RadiusR.DB.Enums;
using RadiusR.DB.ModelExtentions;
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
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "AgentWebService" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select AgentWebService.svc or AgentWebService.svc.cs at the Solution Explorer and start debugging.

    [ServiceBehavior(AddressFilterMode = AddressFilterMode.Any)]
    public class AgentWebService : GenericCustomerService, IAgentWebService
    {
        WebServiceLogger Errorslogger = new WebServiceLogger("PartnerErrors");
        WebServiceLogger InComingInfoLogger = new WebServiceLogger("PartnerInComingInfo");
        //public string GetKeyFragment(string username)
        //{
        //    return KeyManager.GenerateKeyFragment(username, Properties.Settings.Default.CacheDuration);
        //}

        public AgentServiceAuthenticationResponse Authenticate(AgentServiceAuthenticationRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceAuthenticationResponse(passwordHash, request)
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
                    var dbAgents = db.Agents.FirstOrDefault(p => p.Email == request.AuthenticationParameters.UserEmail);
                    if (dbAgents == null)
                    {
                        return new AgentServiceAuthenticationResponse(passwordHash, request)
                        {
                            AuthenticationResponse = new AuthenticationResponse()
                            {
                                IsAuthenticated = false
                            },
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    if (!dbAgents.IsEnabled)
                    {
                        return new AgentServiceAuthenticationResponse(passwordHash, request)
                        {
                            AuthenticationResponse = new AuthenticationResponse()
                            {
                                IsAuthenticated = false
                            },
                            ResponseMessage = CommonResponse.PartnerIsNotActive(request.Culture)
                        };
                    }

                    var partnerPasswordHash = dbAgents.Password;
                    if (partnerPasswordHash.ToLower() != request.AuthenticationParameters.PasswordHash.ToLower())
                    {
                        Errorslogger.LogException(request.Username, new Exception("Wrong passwordHash"));
                        return new AgentServiceAuthenticationResponse(passwordHash, request)
                        {
                            AuthenticationResponse = new AuthenticationResponse()
                            {
                                IsAuthenticated = false
                            },
                            ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                        };
                    }
                    return new AgentServiceAuthenticationResponse(passwordHash, request)
                    {
                        AuthenticationResponse = new AuthenticationResponse()
                        {
                            IsAuthenticated = true,
                            AgentId = dbAgents.ID,
                            DisplayName = dbAgents.CompanyTitle,
                            PhoneNo = dbAgents.PhoneNo,
                            SetupServiceHash = dbAgents.CustomerSetupUser.Password,
                            SetupServiceUser = dbAgents.CustomerSetupUser.Username
                        },
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceAuthenticationResponse(passwordHash, request)
                {
                    AuthenticationResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public AgentServiceSubscriptionsResponse GetAgentSubscriptions(AgentServiceSubscriptionsRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceSubscriptionsResponse(passwordHash, request)
                    {
                        AgentSubscriptionList = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.Where(p => p.Email == request.SubscriptionsRequestParameters.UserEmail).FirstOrDefault();
                    if (dbAgent == null)
                    {
                        return new AgentServiceSubscriptionsResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            AgentSubscriptionList = null
                        };
                    }
                    var agentSubscriptions = dbAgent.Subscriptions.ToList();
                    return new AgentServiceSubscriptionsResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        AgentSubscriptionList = agentSubscriptions?.Select(ps => new AgentSubscriptionsResponse()
                        {
                            ID = ps.ID,
                            CustomerState = new NameValuePair()
                            {
                                Value = ps.State,
                                Name = new LocalizedList<CustomerState, RadiusR.Localization.Lists.CustomerState>().GetDisplayText(ps.State, CreateCulture(request.Culture))
                            },
                            DisplayName = ps.ValidDisplayName,
                            MembershipDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateString(ps.MembershipDate),
                            ExpirationDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateString(ps.RadiusAuthorization.ExpirationDate),
                            SubscriberNo = ps.SubscriberNo
                        }).ToArray()
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceSubscriptionsResponse(passwordHash, request)
                {
                    AgentSubscriptionList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }

        public AgentServiceNewCustomerRegisterResponse NewCustomerRegister(AgentServiceNewCustomerRegisterRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
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
                    //    return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
                    //    {
                    //        NewCustomerRegisterResponse = null,
                    //        ResponseMessage = CommonResponse.SpecialOfferError(request.Culture)
                    //    };
                    //}                    
                    var dbAgent = db.Agents.Where(a => a.Email == request.CustomerRegisterParameters.UserEmail).FirstOrDefault();
                    if (dbAgent == null)
                    {
                        return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
                        {
                            NewCustomerRegisterResponse = null,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    var availableTariffs = dbAgent.AgentTariffs.FirstOrDefault(s => s.Service.ID == request.CustomerRegisterParameters.SubscriptionInfo.ServiceID);
                    if (availableTariffs == null)
                    {
                        return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.TariffNotFound(request.Culture),
                            NewCustomerRegisterResponse = null
                        };
                    }
                    //var externalTariff = db.ExternalTariffs.GetActiveExternalTariffs().FirstOrDefault(ext => ext.TariffID == availableTariffs.TariffID);
                    //if (externalTariff == null)
                    //{
                    //    return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
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
                            AgentID = dbAgent.ID,
                            RegistrationType = (SubscriptionRegistrationType)register.ExtraInfo.ApplicationType,
                            TransitionPSTN = register.ExtraInfo.PSTN,
                            TransitionXDSLNo = register.ExtraInfo.XDSLNo,
                            DomainID = availableTariffs.DomainID,
                            ServiceID = availableTariffs.Service.ID,
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
                            return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
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
                        return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
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
                            return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
                            {
                                NewCustomerRegisterResponse = valuePairs,
                                ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                            };
                        }
                        db.SaveChanges();
                        db.SystemLogs.Add(RadiusR.SystemLogs.SystemLogProcessor.AddSubscription(null, dbCustomer.Subscriptions.FirstOrDefault().ID, dbCustomer.ID, SubscriptionRegistrationType.NewRegistration, SystemLogInterface.PartnerWebService, request.Username, dbCustomer.Subscriptions.FirstOrDefault().SubscriberNo));
                        db.SaveChanges();
                        return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
                        {
                            NewCustomerRegisterResponse = valuePairs,
                            ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        };
                    }
                    ////if (registeredCustomer == null)
                    ////{
                    ////    //exist customer
                    ////    //return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
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
                return new AgentServiceNewCustomerRegisterResponse(passwordHash, request)
                {
                    NewCustomerRegisterResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public AgentServiceSMSCodeResponse SendConfirmationSMS(AgentServiceSMSCodeRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceSMSCodeResponse(passwordHash, request)
                    {
                        SMSCodeResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var phoneNoRegex = new Regex(@"^[1-9]{1}[0-9]{9}$", RegexOptions.ECMAScript);
                if (request.SMSCodeRequest.PhoneNo == null || !phoneNoRegex.IsMatch(request.SMSCodeRequest.PhoneNo))
                {
                    return new AgentServiceSMSCodeResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.PartnerInvalidPhoneNoResponse(request.Culture),
                        SMSCodeResponse = null
                    };
                }

                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.FirstOrDefault(p => p.Email == request.SMSCodeRequest.UserEmail);
                    if (dbAgent == null)
                    {
                        return new AgentServiceSMSCodeResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            SMSCodeResponse = null
                        };
                    }
                    var code = new Random().Next(100000, 1000000).ToString("000000");
                    SMSService smsClient = new SMSService();
                    smsClient.SendGenericSMS(request.SMSCodeRequest.PhoneNo, request.Culture, RadiusR.DB.Enums.SMSType.OperationCode, new Dictionary<string, object>() { { SMSParamaterRepository.SMSParameterNameCollection.SMSCode, code } });
                    return new AgentServiceSMSCodeResponse(passwordHash, request)
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
                return new AgentServiceSMSCodeResponse(passwordHash, request)
                {
                    SMSCodeResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public AgentServiceKeyValueListResponse GetCultures(AgentServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }
                var results = new KeyValueItem[] { new KeyValueItem() { Key = 0, Value = "en-US" }, new KeyValueItem() { Key = 0, Value = "tr-tr" } };
                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public AgentServiceKeyValueListResponse GetTCKTypes(AgentServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var list = new LocalizedList<RadiusR.DB.Enums.IDCardTypes, RadiusR.Localization.Lists.IDCardTypes>();
                var results = list.GetList(CreateCulture(request.Culture)).Select(item => new KeyValueItem() { Key = item.Key, Value = item.Value }).ToArray();

                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public AgentServiceKeyValueListResponse GetCustomerTypes(AgentServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var list = new LocalizedList<RadiusR.DB.Enums.CustomerType, RadiusR.Localization.Lists.CustomerType>();
                var results = list.GetList(CreateCulture(request.Culture)).Select(item => new KeyValueItem() { Key = item.Key, Value = item.Value }).ToArray();

                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public AgentServiceKeyValueListResponse GetSexes(AgentServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var list = new LocalizedList<RadiusR.DB.Enums.Sexes, RadiusR.Localization.Lists.Sexes>();
                var results = list.GetList(CreateCulture(request.Culture)).Select(item => new KeyValueItem() { Key = item.Key, Value = item.Value }).ToArray();

                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public AgentServiceKeyValueListResponse GetNationalities(AgentServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var list = new LocalizedList<RadiusR.DB.Enums.CountryCodes, RadiusR.Localization.Lists.CountryCodes>();
                var results = list.GetList(CreateCulture(request.Culture)).Select(item => new KeyValueItem() { Key = item.Key, Value = item.Value }).ToArray();

                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public AgentServiceKeyValueListResponse GetProfessions(AgentServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                var list = new LocalizedList<RadiusR.DB.Enums.Profession, RadiusR.Localization.Lists.Profession>();
                var results = list.GetList(CreateCulture(request.Culture)).Select(item => new KeyValueItem() { Key = item.Key, Value = item.Value }).ToArray();

                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = results,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public AgentServiceKeyValueListResponse GetAgentTariffs(AgentServiceParameterlessRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                using (RadiusREntities db = new RadiusREntities())
                {
                    //Include(p => p.PartnerGroup.PartnerAvailableTariffs.Select(pat => pat.Service.Domains)).
                    var dbAgent = db.Agents.FirstOrDefault(p => p.Email == request.ParameterlessRequest.UserEmail);
                    if (dbAgent == null)
                    {
                        return new AgentServiceKeyValueListResponse(passwordHash, request)
                        {
                            KeyValueItemResponse = null,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    var rawResults = dbAgent.AgentTariffs.Select(pat => new { ID = pat.Service.ID, TariffName = pat.Service.Name, DomainName = pat.Domain.Name }).ToArray();
                    var list = new LocalizedList<RadiusR.DB.Enums.CommitmentLength, RadiusR.Localization.Lists.CommitmentLength>();
                    var results = rawResults.Select(rr => new KeyValueItem()
                    {
                        Key = rr.ID,
                        Value = rr.TariffName + "(" + rr.DomainName + ")"
                    }).ToArray();

                    return new AgentServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = results,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public AgentServiceKeyValueListResponse GetPaymentDays(AgentServiceListFromIDRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.FirstOrDefault(p => p.Email == request.ListFromIDRequest.UserEmail);

                    var results = new KeyValueItem[0];
                    var availableTariff = dbAgent.AgentTariffs.FirstOrDefault(pat => pat.TariffID == request.ListFromIDRequest.ID);
                    if (availableTariff != null)
                    {
                        results = availableTariff.Service.ServiceBillingPeriods.Select(sbp => new KeyValueItem() { Key = sbp.DayOfMonth, Value = sbp.DayOfMonth.ToString() }).ToArray();
                    }
                    return new AgentServiceKeyValueListResponse(passwordHash, request)
                    {
                        KeyValueItemResponse = results,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceKeyValueListResponse(passwordHash, request)
                {
                    KeyValueItemResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }

        public AgentServicePaymentResponse PayBills(AgentServicePaymentRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServicePaymentResponse(passwordHash, request)
                    {
                        PaymentResponse = false,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }
                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.FirstOrDefault(p => p.Email == request.PaymentRequest.UserEmail);
                    if (dbAgent == null)
                    {
                        return new AgentServicePaymentResponse(passwordHash, request)
                        {
                            PaymentResponse = false,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    if (!string.IsNullOrEmpty(request.PaymentRequest.PrePaidSubscription))
                    {
                        var dbSubscription = db.Subscriptions.FirstOrDefault(s => s.SubscriberNo == request.PaymentRequest.PrePaidSubscription);
                        var payResponse = RadiusR.DB.Utilities.Billing.ExtendPackage.ExtendClientPackage(db, dbSubscription, 1, PaymentType.Cash, BillPayment.AccountantType.Admin);
                        db.SystemLogs.Add(RadiusR.SystemLogs.SystemLogProcessor.ExtendPackage(null, dbSubscription.ID, SystemLogInterface.CustomerWebsite, request.Username, 1));
                        db.SaveChanges();
                        if (payResponse == BillPayment.ResponseType.Success)
                        {
                            return new AgentServicePaymentResponse(passwordHash, request)
                            {
                                PaymentResponse = true,
                                ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                            };
                        }
                        return new AgentServicePaymentResponse(passwordHash, request)
                        {
                            PaymentResponse = false,
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture),
                        };
                    }
                    if (request.PaymentRequest.BillIDs == null || request.PaymentRequest.BillIDs.Count() <= 0)
                    {
                        return new AgentServicePaymentResponse(passwordHash, request)
                        {
                            PaymentResponse = false,
                            ResponseMessage = CommonResponse.BillsNotFoundException(request.Culture)
                        };
                    }
                    /*
                    .Include(bill => bill.Subscription)
                        .Include(bill => bill.BillFees.Select(bf => bf.Discount))
                     */
                    var dbBills = db.Bills.Where(bill => request.PaymentRequest.BillIDs.Contains(bill.ID) && bill.BillStatusID == (short)RadiusR.DB.Enums.BillState.Unpaid).ToArray();

                    if (dbBills.Count() != request.PaymentRequest.BillIDs.Count())
                    {
                        return new AgentServicePaymentResponse(passwordHash, request)
                        {
                            PaymentResponse = false,
                            ResponseMessage = CommonResponse.BillsNotFoundException(request.Culture)
                        };
                    }
                    // pay bills                    
                    var paymentResults = db.PayBills(dbBills, DB.Enums.PaymentType.Partner, BillPayment.AccountantType.Admin, gateway: new BillPaymentGateway()
                    {
                        PaymentAgent = dbAgent
                    });

                    if (paymentResults != BillPayment.ResponseType.Success)
                    {
                        if (paymentResults == BillPayment.ResponseType.NotEnoughCredit)
                        {
                            return new AgentServicePaymentResponse(passwordHash, request)
                            {
                                PaymentResponse = false,
                                ResponseMessage = CommonResponse.NotEnoughCredit(request.Culture)
                            };
                        }
                        return new AgentServicePaymentResponse(passwordHash, request)
                        {
                            PaymentResponse = false,
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture)
                        };
                    }
                    // set system logs
                    var gatewayName = dbAgent.CompanyTitle + (dbAgent != null ? " (" + dbAgent.ExecutiveName + ")" : string.Empty);
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
                    return new AgentServicePaymentResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        PaymentResponse = true, //dbBills.Select(b => b.ID).ToArray()
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServicePaymentResponse(passwordHash, request)
                {
                    PaymentResponse = false,
                    ResponseMessage = CommonResponse.InternalException(request.Username)
                };
            }
        }
        public AgentServiceCredentialSMSResponse SendCredentialSMS(AgentServiceCredentialSMSRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceCredentialSMSResponse(passwordHash, request)
                    {
                        CredentialSMSResponse = false,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.FirstOrDefault(a => a.Email == request.CredentialSMSParameter.UserEmail);
                    if (dbAgent == null)
                    {
                        return new AgentServiceCredentialSMSResponse(passwordHash, request)
                        {
                            CredentialSMSResponse = false,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    var dbSubscription = dbAgent.Subscriptions.Where(s => s.SubscriberNo == request.CredentialSMSParameter.SubscriberNo).FirstOrDefault();
                    if (dbSubscription == null)
                    {
                        return new AgentServiceCredentialSMSResponse(passwordHash, request)
                        {
                            CredentialSMSResponse = false,
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture)
                        };
                    }
                    SMSService SMS = new SMSService();
                    db.SMSArchives.AddSafely(SMS.SendSubscriberSMS(dbSubscription, SMSType.UserCredentials));
                    db.SaveChanges();
                    return new AgentServiceCredentialSMSResponse(request.Culture, request)
                    {
                        CredentialSMSResponse = true,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceCredentialSMSResponse(passwordHash, request)
                {
                    CredentialSMSResponse = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public AgentServiceIDCardValidationResponse IDCardValidation(AgentServiceIDCardValidationRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceIDCardValidationResponse(passwordHash, request)
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
                    return new AgentServiceIDCardValidationResponse(passwordHash, request)
                    {
                        IDCardValidationResponse = result,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
                else
                {
                    if (request.IDCardValidationRequest.RegistirationNo.Length != 9)
                    {
                        return new AgentServiceIDCardValidationResponse(passwordHash, request)
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
                    return new AgentServiceIDCardValidationResponse(passwordHash, request)
                    {
                        IDCardValidationResponse = result,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceIDCardValidationResponse(passwordHash, request)
                {
                    IDCardValidationResponse = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public AgentServiceBillListResponse GetBills(AgentServiceBillListRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);

                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceBillListResponse(passwordHash, request)
                    {
                        BillListResponse = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request)
                    };
                }

                using (RadiusREntities db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.FirstOrDefault(p => p.Email == request.BillListRequest.UserEmail);
                    if (dbAgent == null)
                    {
                        return new AgentServiceBillListResponse(passwordHash, request)
                        {
                            BillListResponse = null,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    //var dbSubscriber = db.Subscriptions.FirstOrDefault(s => s.SubscriberNo == request.BillListRequest.SubscriberNo);
                    var customerCode = request.BillListRequest.CustomerCode.StartsWith("0") ? request.BillListRequest.CustomerCode.TrimStart('0') : request.BillListRequest.CustomerCode;
                    var dbSubscriber = db.Subscriptions.Where(s => s.SubscriberNo == customerCode || s.Customer.CustomerIDCard.TCKNo == customerCode
                    || s.Customer.ContactPhoneNo == customerCode).ToArray();
                    dbSubscriber = dbSubscriber.Where(s => s.AgentID == dbAgent.ID).ToArray();
                    if (dbSubscriber == null || dbSubscriber.Length == 0)
                    {
                        return new AgentServiceBillListResponse(passwordHash, request)
                        {
                            BillListResponse = null,
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture)
                        };
                    }
                    var dbSubscriberId = dbSubscriber.Select(d => d.ID).ToArray();
                    var bills = db.Bills.Where(b => dbSubscriberId.Contains(b.SubscriptionID) && b.BillStatusID == (short)RadiusR.DB.Enums.BillState.Unpaid).OrderBy(b => b.ID).ToArray();
                    var results = bills.Select(b => new BillListResponse.BillInfo()
                    {
                        SubscriberNo = b.Subscription.SubscriberNo,
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
                    var totalCredits = dbSubscriber.SelectMany(db => db.SubscriptionCredits).Select(sc => sc.Amount).DefaultIfEmpty(0m).Sum();
                    var prePaidSubscriber = dbSubscriber.Where(s => s.Service.BillingType == (short)RadiusR.DB.Enums.BillType.PrePaid)
                        .Select(s => new BillListResponse.PrePaidSubscriptionInfo() { SubscriberNo = s.SubscriberNo, ServiceName = s.Service.Name, Total = s.Service.Price }).ToArray();
                    return new AgentServiceBillListResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        BillListResponse = new BillListResponse()
                        {
                            PrePaidSubscriptionInfoes = prePaidSubscriber,
                            Bills = results,
                            SubscriberName = dbSubscriber.FirstOrDefault().ValidDisplayName,
                            TotalCredits = totalCredits
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceBillListResponse(passwordHash, request)
                {
                    BillListResponse = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        #region
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
        private int TotalPageCount(int? TotalRow, int? itemPerPage)
        {
            itemPerPage = itemPerPage ?? 10;
            var count = !TotalRow.HasValue ? 0 : (TotalRow % itemPerPage) == 0 ?
                        (TotalRow / itemPerPage) :
                        (TotalRow / itemPerPage) + 1;
            return count ?? 0;
        }
        #endregion

    }
}

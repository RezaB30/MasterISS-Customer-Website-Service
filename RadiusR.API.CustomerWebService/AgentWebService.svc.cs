using RadiusR.API.CustomerWebService.Requests.AgentRequests;
using RadiusR.API.CustomerWebService.Responses.AgentResponses;
using RadiusR.DB;
using RadiusR.DB.Enums;
using RadiusR.DB.ModelExtentions;
using RadiusR.DB.Utilities.Billing;
using RadiusR.DB.Utilities.ComplexOperations.Subscriptions.Registration;
using RadiusR.DB.Utilities.Extentions;
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
                    var searchFilter = request.SubscriptionsRequestParameters.SearchFilter.CustomerCode;
                    var agentSubscriptions = string.IsNullOrEmpty(searchFilter) ? dbAgent.Subscriptions.ToList()
                        : dbAgent.Subscriptions.Where(s => s.ValidDisplayName.Contains(searchFilter) || s.SubscriberNo.Contains(searchFilter)).ToList();
                    return new AgentServiceSubscriptionsResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        AgentSubscriptionList = new AgentSubscriptionsResponse()
                        {
                            TotalPageCount = TotalPageCount(agentSubscriptions.Count(), request.SubscriptionsRequestParameters.Pagination.ItemPerPage),
                            AgentSubscriptionList = agentSubscriptions?.OrderByDescending(ps => ps.MembershipDate)
                            .Skip((request.SubscriptionsRequestParameters.Pagination.PageNo.Value * request.SubscriptionsRequestParameters.Pagination.ItemPerPage.Value)).Take(request.SubscriptionsRequestParameters.Pagination.ItemPerPage.Value).Select(ps => new AgentSubscriptionsResponse.AgentSubscriptions()
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
                        }
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
                        db.SystemLogs.Add(RadiusR.SystemLogs.SystemLogProcessor.ExtendPackage(null, dbSubscription.ID, SystemLogInterface.PartnerWebService, request.Username, 1));
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
                    //billing
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
                    var paymentResults = db.PayBills(dbBills, DB.Enums.PaymentType.Cash, BillPayment.AccountantType.Admin, gateway: new BillPaymentGateway()
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
                        db.SystemLogs.Add(SystemLogProcessor.BillPayment(group.Select(bill => bill.ID), null, group.Key.ID, RadiusR.DB.Enums.SystemLogInterface.PartnerWebService, request.PaymentRequest.UserEmail, RadiusR.DB.Enums.PaymentType.Cash, gatewayName));
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
        public AgentServiceAddWorkOrderResponse AddWorkOrder(AgentServiceAddWorkOrderRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceAddWorkOrderResponse(passwordHash, request)
                    {
                        AddWorkOrderResult = false,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.FirstOrDefault(a => a.Email == request.AddWorkOrder.UserEmail);
                    if (dbAgent == null)
                    {
                        return new AgentServiceAddWorkOrderResponse(passwordHash, request)
                        {
                            AddWorkOrderResult = false,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    var subscription = dbAgent.Subscriptions.FirstOrDefault(s => s.ID == request.AddWorkOrder.SubscriptionId);
                    if (subscription == null)
                    {
                        return new AgentServiceAddWorkOrderResponse(passwordHash, request)
                        {
                            AddWorkOrderResult = false,
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture)
                        };
                    }
                    var setupUser = db.CustomerSetupUsers.Find(request.AddWorkOrder.SetupUserId);
                    if (setupUser == null)
                    {
                        return new AgentServiceAddWorkOrderResponse(passwordHash, request)
                        {
                            AddWorkOrderResult = false,
                            ResponseMessage = CommonResponse.FailedResponse(request.Culture, "Setup User Not Found")
                        };
                    }
                    request.AddWorkOrder.HasModem = !request.AddWorkOrder.HasModem.HasValue ? false : request.AddWorkOrder.HasModem;
                    request.AddWorkOrder.ModemName = request.AddWorkOrder.HasModem.GetValueOrDefault(false) ? request.AddWorkOrder.ModemName : null;
                    var dbWorkOrder = new CustomerSetupTask()
                    {
                        SubscriptionID = subscription.ID,
                        Details = request.AddWorkOrder.Description,
                        HasModem = request.AddWorkOrder.HasModem.GetValueOrDefault(false),
                        ModemName = request.AddWorkOrder.ModemName,
                        SetupUserID = request.AddWorkOrder.SetupUserId,
                        TaskType = request.AddWorkOrder.TaskType,
                        XDSLType = request.AddWorkOrder.XDSLType,
                        TaskIssueDate = DateTime.Now,
                        TaskStatus = (short)RadiusR.DB.Enums.CustomerSetup.TaskStatuses.New,
                        Allowance = setupUser.Partners.Any() ? setupUser.Partners.First().SetupAllowance : (decimal?)null,
                        AllowanceState = (short)PartnerAllowanceState.OnHold
                    };
                    db.CustomerSetupTasks.Add(dbWorkOrder);

                    db.SaveChanges();
                    db.SystemLogs.Add(SystemLogProcessor.AddWorkOrder(dbWorkOrder.ID, null, subscription.ID, SystemLogInterface.MasterISS, null));
                    db.SaveChanges();
                    return new AgentServiceAddWorkOrderResponse(passwordHash, request)
                    {
                        AddWorkOrderResult = true,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceAddWorkOrderResponse(passwordHash, request)
                {
                    AddWorkOrderResult = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public AgentServiceServiceOperatorsResponse ServiceOperators(AgentServiceServiceOperatorsRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceServiceOperatorsResponse(passwordHash, request)
                    {
                        ServiceOperators = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.FirstOrDefault(a => a.Email == request.ServiceOperatorsParameters.UserEmail);
                    if (dbAgent == null)
                    {
                        return new AgentServiceServiceOperatorsResponse(passwordHash, request)
                        {
                            ServiceOperators = null,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    var subscription = dbAgent.Subscriptions.FirstOrDefault(s => s.ID == request.ServiceOperatorsParameters.SubscriptionId);
                    if (subscription == null)
                    {
                        return new AgentServiceServiceOperatorsResponse(passwordHash, request)
                        {
                            ServiceOperators = null,
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture)
                        };
                    }
                    var serviceOperators = db.CustomerSetupUsers.ActiveUsers().ValidAgents(dbAgent.ID).OrderBy(user => user.Name).Select(user => new NameValuePair { Value = user.ID, Name = user.Name }).ToArray();
                    return new AgentServiceServiceOperatorsResponse(passwordHash, request)
                    {
                        ServiceOperators = serviceOperators,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceServiceOperatorsResponse(passwordHash, request)
                {
                    ServiceOperators = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public AgentServiceCustomerSetupTaskResponse GetCustomerTasks(AgentServiceCustomerSetupTaskRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceCustomerSetupTaskResponse(passwordHash, request)
                    {
                        CustomerTaskList = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.FirstOrDefault(a => a.Email == request.CustomerTaskParameters.UserEmail);
                    if (dbAgent == null)
                    {
                        return new AgentServiceCustomerSetupTaskResponse(passwordHash, request)
                        {
                            CustomerTaskList = null,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    var subscription = dbAgent.Subscriptions.FirstOrDefault(s => s.ID == request.CustomerTaskParameters.SubscriptionId);
                    if (subscription == null)
                    {
                        return new AgentServiceCustomerSetupTaskResponse(passwordHash, request)
                        {
                            CustomerTaskList = null,
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture)
                        };
                    }
                    var getCustomerTasks = db.CustomerSetupTasks.Where(task => task.SubscriptionID == subscription.ID).ToArray();
                    var customerTasks = getCustomerTasks?.Select(task => new CustomerSetupTaskResponse()
                    {
                        ID = task.ID,
                        ValidDisplayName = task.Subscription.Customer.ValidDisplayName,
                        Allowance = task.Allowance,
                        CompletionDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(task.CompletionDate),
                        Details = task.Details,
                        HasModem = task.HasModem,
                        SubscriptionID = task.SubscriptionID,
                        TaskType = new NameValuePair()
                        {
                            Value = task.TaskType,
                            Name = new LocalizedList<RadiusR.DB.Enums.CustomerSetup.TaskTypes, RadiusR.Localization.Lists.CustomerSetup.TaskType>()
                            .GetDisplayText(task.TaskType, CreateCulture(request.Culture))
                        },
                        AllowanceState = new NameValuePair()
                        {
                            Value = task.AllowanceState,
                            Name = new LocalizedList<RadiusR.DB.Enums.PartnerAllowanceState, RadiusR.Localization.Lists.PartnerAllowanceState>()
                            .GetDisplayText(task.AllowanceState, CreateCulture(request.Culture))
                        },
                        TaskStatus = new NameValuePair()
                        {
                            Value = task.TaskStatus,
                            Name = new LocalizedList<RadiusR.DB.Enums.CustomerSetup.TaskStatuses, RadiusR.Localization.Lists.CustomerSetup.TaskStatuses>()
                            .GetDisplayText(task.TaskStatus, CreateCulture(request.Culture))
                        },
                        ModemName = task.ModemName,
                        SetupUser = new NameValuePair()
                        {
                            Name = db.CustomerSetupUsers.Find(task.SetupUserID) == null ? string.Empty : db.CustomerSetupUsers.Find(task.SetupUserID).Name,
                            Value = task.SetupUserID
                        },
                        TaskIssueDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(task.TaskIssueDate),
                        CustomerTaskUpdates = task.CustomerSetupStatusUpdates.Count() == 0 ? null : task.CustomerSetupStatusUpdates.Select(u => new CustomerSetupTaskResponse.TaskUpdates()
                        {
                            Date = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(u.Date),
                            Description = u.Description,
                            FaultCode = new NameValuePair()
                            {
                                Value = u.FaultCode,
                                Name = new LocalizedList<RadiusR.DB.Enums.CustomerSetup.FaultCodes, RadiusR.Localization.Lists.CustomerSetup.FaultCodes>()
                            .GetDisplayText(u.FaultCode, CreateCulture(request.Culture))
                            },
                            ReservationDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(u.ReservationDate),
                            ID = u.ID
                        }).ToArray()
                    }).ToArray();
                    return new AgentServiceCustomerSetupTaskResponse(passwordHash, request)
                    {
                        CustomerTaskList = customerTasks,
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                    };
                }
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceCustomerSetupTaskResponse(passwordHash, request)
                {
                    CustomerTaskList = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture)
                };
            }
        }
        public AgentServiceClientFormsResponse GetAgentClientForms(AgentServiceClientFormsRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceClientFormsResponse(passwordHash, request)
                    {
                        AgentClientForms = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.Where(p => p.Email == request.ClientFormsParameters.UserEmail).FirstOrDefault();
                    if (dbAgent == null)
                    {
                        return new AgentServiceClientFormsResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            AgentClientForms = null
                        };
                    }
                    if (request.ClientFormsParameters.SubscriptionId == null)
                    {
                        return new AgentServiceClientFormsResponse(passwordHash, request)
                        {
                            AgentClientForms = null,
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture)
                        };
                    }
                    var subscription = dbAgent.Subscriptions.FirstOrDefault(s => s.ID == request.ClientFormsParameters.SubscriptionId);
                    if (subscription == null)
                    {
                        return new AgentServiceClientFormsResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture),
                            AgentClientForms = null
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
                                return new AgentServiceClientFormsResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                    AgentClientForms = new AgentClientFormsResponse()
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
                                return new AgentServiceClientFormsResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                    AgentClientForms = new AgentClientFormsResponse()
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
                                return new AgentServiceClientFormsResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                    AgentClientForms = new AgentClientFormsResponse()
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
                                return new AgentServiceClientFormsResponse(passwordHash, request)
                                {
                                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                                    AgentClientForms = null
                                };
                            }
                    }
                }

            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceClientFormsResponse(passwordHash, request)
                {
                    AgentClientForms = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public AgentServiceSaveClientAttachmentResponse SaveClientAttachment(AgentServiceSaveClientAttachmentRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceSaveClientAttachmentResponse(password, request)
                    {
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                        SaveClientAttachmentResult = false
                    };
                }
                if (request.SaveClientAttachmentParameters == null || request.SaveClientAttachmentParameters.SubscriptionId == null)
                {
                    return new AgentServiceSaveClientAttachmentResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture),
                        SaveClientAttachmentResult = false
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.FirstOrDefault(a => a.Email == request.SaveClientAttachmentParameters.UserEmail);
                    if (dbAgent == null)
                    {
                        return new AgentServiceSaveClientAttachmentResponse(passwordHash, request)
                        {
                            SaveClientAttachmentResult = false,
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture)
                        };
                    }
                    var subscription = dbAgent.Subscriptions.FirstOrDefault(s => s.ID == request.SaveClientAttachmentParameters.SubscriptionId);
                    if (subscription == null)
                    {
                        return new AgentServiceSaveClientAttachmentResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture),
                            SaveClientAttachmentResult = false
                        };
                    }
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
                    return new AgentServiceSaveClientAttachmentResponse(passwordHash, request)
                    {
                        SaveClientAttachmentResult = false,
                        ResponseMessage = CommonResponse.FailedResponse(request.Culture, saveAttachment.InternalException.Message)
                    };
                }
                return new AgentServiceSaveClientAttachmentResponse(passwordHash, request)
                {
                    SaveClientAttachmentResult = saveAttachment.Result,
                    ResponseMessage = CommonResponse.SuccessResponse(request.Culture)
                };
            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceSaveClientAttachmentResponse(passwordHash, request)
                {
                    SaveClientAttachmentResult = false,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public AgentServiceBillReceiptResponse GetBillReceipt(AgentServiceBillReceiptRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceBillReceiptResponse(passwordHash, request)
                    {
                        BillReceiptResult = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.Where(p => p.Email == request.BillReceiptParameters.UserEmail).FirstOrDefault();
                    if (dbAgent == null)
                    {
                        return new AgentServiceBillReceiptResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            BillReceiptResult = null
                        };
                    }
                    if (request.BillReceiptParameters.BillId == null)
                    {
                        return new AgentServiceBillReceiptResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture),
                            BillReceiptResult = null
                        };
                    }
                    var bill = db.Bills.Find(request.BillReceiptParameters.BillId);
                    if (bill == null)
                    {
                        return new AgentServiceBillReceiptResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerSubscriberNotFoundResponse(request.Culture),
                            BillReceiptResult = null
                        };
                    }
                    var dbSubscription = bill.Subscription;
                    var createdPDF = RadiusR.PDFForms.PDFWriter.GetBillReceiptPDF(db, dbSubscription.ID, bill.ID, CreateCulture(request.Culture));
                    byte[] content = null;
                    using (var memoryStream = new MemoryStream())
                    {
                        createdPDF.Result.CopyTo(memoryStream);
                        content = memoryStream.ToArray();
                    }
                    return new AgentServiceBillReceiptResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        BillReceiptResult = new BillReceiptResponse()
                        {
                            FileContent = content,
                            FileName = new LocalizedList<PDFFormType, RadiusR.Localization.Lists.PDFFormType>()
                            .GetDisplayText((int)PDFFormType.BillReceipt, CreateCulture(request.Culture)),
                            MIMEType = "application/pdf"
                        }
                    };
                }

            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceBillReceiptResponse(passwordHash, request)
                {
                    BillReceiptResult = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
                };
            }
        }
        public AgentServiceRelatedPaymentsResponse GetRelatedPayments(AgentServiceRelatedPaymentsRequest request)
        {
            var password = new ServiceSettings().GetAgentUserPassword(request.Username);
            var passwordHash = HashUtilities.GetHexString<SHA256>(password);
            try
            {
                InComingInfoLogger.LogIncomingMessage(request);
                if (!request.HasValidHash(passwordHash, Properties.Settings.Default.CacheDuration))
                {
                    return new AgentServiceRelatedPaymentsResponse(passwordHash, request)
                    {
                        RelatedPayments = null,
                        ResponseMessage = CommonResponse.PartnerUnauthorizedResponse(request),
                    };
                }
                using (var db = new RadiusREntities())
                {
                    var dbAgent = db.Agents.Where(p => p.Email == request.RelatedPaymentsParameters.UserEmail).FirstOrDefault();
                    if (dbAgent == null)
                    {
                        return new AgentServiceRelatedPaymentsResponse(passwordHash, request)
                        {
                            ResponseMessage = CommonResponse.PartnerNotFoundResponse(request.Culture),
                            RelatedPayments = null
                        };
                    }
                    var agentSubscriptionBills = dbAgent.Subscriptions.SelectMany(s => s.Bills).ToArray();
                    var relatedPayments = agentSubscriptionBills
                        .Where(b => b.BillStatusID == (short)BillState.Paid).OrderByDescending(b => b.PayDate).ToList();
                    relatedPayments = string.IsNullOrEmpty(request.RelatedPaymentsParameters.SearchFilter.CustomerCode) ? relatedPayments
                        : relatedPayments.Where(r => r.Subscription.Customer.ValidDisplayName.Contains(request.RelatedPaymentsParameters.SearchFilter.CustomerCode) || r.Subscription.SubscriberNo.Contains(request.RelatedPaymentsParameters.SearchFilter.CustomerCode)).ToList();
                    //var relatedPayments = dbAgent.AgentRelatedPayments.ToList();
                    var itemPerPage = request.RelatedPaymentsParameters.Pagination.ItemPerPage;
                    var pageNo = request.RelatedPaymentsParameters.Pagination.PageNo;
                    return new AgentServiceRelatedPaymentsResponse(passwordHash, request)
                    {
                        ResponseMessage = CommonResponse.SuccessResponse(request.Culture),
                        RelatedPayments = new RelatedPaymentsResponse()
                        {
                            TotalPageCount = TotalPageCount(relatedPayments.Count(), request.RelatedPaymentsParameters.Pagination.ItemPerPage),
                            RelatedPaymentList = relatedPayments?.OrderByDescending(p => p.PayDate).Skip(pageNo.Value * itemPerPage.Value).Take(itemPerPage.Value).Select(p => new RelatedPaymentsResponse.RelatedPayments()
                            {
                                BillID = p.ID,
                                Cost = p.GetPayableCost(),
                                Description = string.Join(",", p.BillFees?.Select(bf => bf.Description)),
                                ValidDisplayName = p.Subscription.Customer.ValidDisplayName,
                                IssueDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateString(p.IssueDate),
                                PayDate = RezaB.API.WebService.DataTypes.ServiceTypeConverter.GetDateTimeString(p.PayDate),
                                SubscriberNo = p.Subscription.SubscriberNo,
                            }).ToArray()
                        }
                    };
                }

            }
            catch (Exception ex)
            {
                Errorslogger.LogException(request.Username, ex);
                return new AgentServiceRelatedPaymentsResponse(passwordHash, request)
                {
                    RelatedPayments = null,
                    ResponseMessage = CommonResponse.InternalException(request.Culture, ex),
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
            int? count = 0;
            try
            {
                itemPerPage = itemPerPage ?? 10;
                count = !TotalRow.HasValue ? 0 : (TotalRow % itemPerPage) == 0 ?
                            (TotalRow / itemPerPage) :
                            (TotalRow / itemPerPage) + 1;
            }
            catch { }

            return count ?? 0;
        }

        #endregion

    }
}

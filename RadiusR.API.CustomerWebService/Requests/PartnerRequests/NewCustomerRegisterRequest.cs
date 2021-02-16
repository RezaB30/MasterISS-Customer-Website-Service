using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.PartnerRequests
{
    [DataContract]
    public class NewCustomerRegisterRequest : RequestBase
    {
        [DataMember]
        public IDCardInfo IDCardInfo { get; set; }
        [DataMember]
        public CustomerGeneralInfo CustomerGeneralInfo { get; set; }
        [DataMember]
        public IndividualCustomerInfo IndividualCustomerInfo { get; set; }
        [DataMember]
        public CorporateCustomerInfo CorporateCustomerInfo { get; set; }
        [DataMember]
        public SubscriptionRegistrationInfo SubscriptionInfo { get; set; }

    }
    [DataContract]
    public class IDCardInfo
    {
        [DataMember]
        public int? CardType { get; set; }
        [DataMember]
        public string PassportNo { get; set; }
        [DataMember]
        public string FirstName { get; set; }
        [DataMember]
        public string LastName { get; set; }
        [DataMember]
        public string TCKNo { get; set; }
        [DataMember]
        public string VolumeNo { get; set; }
        [DataMember]
        public string RowNo { get; set; }
        [DataMember]
        public string PageNo { get; set; }
        [DataMember]
        public string Province { get; set; }
        [DataMember]
        public string District { get; set; }
        [DataMember]
        public string Neighbourhood { get; set; }
        [DataMember]
        public string SerialNo { get; set; }
        [DataMember]
        public string PlaceOfIssue { get; set; }
        [DataMember]
        public string DateOfIssue { get; set; } // datetime
        [DataMember]
        public string BirthDate { get; set; } // datetime
    }
    [DataContract]
    public class CustomerGeneralInfo
    {
        [DataMember]
        public int? CustomerType { get; set; }
        [DataMember]
        public string Email { get; set; }
        [DataMember]
        public string Culture { get; set; }
        [DataMember]
        public string ContactPhoneNo { get; set; }
        [DataMember]
        public PhoneNoListItem[] OtherPhoneNos { get; set; }
        [DataMember]
        public AddressInfo BillingAddress { get; set; }
    }
    [DataContract]
    public class PhoneNoListItem
    {
        [DataMember]
        public string Number { get; set; }
    }
    [DataContract]
    public class AddressInfo
    {
        [DataMember]
        public string AddressText { get; set; }
        [DataMember]
        public string StreetName { get; set; }
        [DataMember]
        public string NeighbourhoodName { get; set; }
        [DataMember]
        public string DistrictName { get; set; }
        [DataMember]
        public string ProvinceName { get; set; }
        [DataMember]
        public long? AddressNo { get; set; }
        [DataMember]
        public string Floor { get; set; }
        [DataMember]
        public int? PostalCode { get; set; }
        [DataMember]
        public string ApartmentNo { get; set; }
        [DataMember]
        public long? ApartmentID { get; set; }
        [DataMember]
        public long? DoorID { get; set; }
        [DataMember]
        public long? StreetID { get; set; }
        [DataMember]
        public long? NeighbourhoodID { get; set; }
        [DataMember]
        public long? RuralCode { get; set; }
        [DataMember]
        public long? DistrictID { get; set; }
        [DataMember]
        public long? ProvinceID { get; set; }
        [DataMember]
        public string DoorNo { get; set; }
    }
    [DataContract]
    public class IndividualCustomerInfo
    {
        [DataMember]
        public int? Sex { get; set; }
        [DataMember]
        public int? Nationality { get; set; }
        [DataMember]
        public string FathersName { get; set; }
        [DataMember]
        public string MothersName { get; set; }
        [DataMember]
        public string MothersMaidenName { get; set; }
        [DataMember]
        public string BirthPlace { get; set; }
        [DataMember]
        public int? Profession { get; set; }
        [DataMember]
        public AddressInfo ResidencyAddress { get; set; }
    }
    [DataContract]
    public class CorporateCustomerInfo
    {

        [DataMember]
        public string Title { get; set; }
        [DataMember]
        public string TaxNo { get; set; }
        [DataMember]
        public string TaxOffice { get; set; }
        [DataMember]
        public string CentralSystemNo { get; set; }
        [DataMember]
        public string TradeRegistrationNo { get; set; }
        [DataMember]
        public string ExecutiveFathersName { get; set; }
        [DataMember]
        public string ExecutiveMothersName { get; set; }
        [DataMember]
        public string ExecutiveMothersMaidenName { get; set; }
        [DataMember]
        public int? ExecutiveSex { get; set; }
        [DataMember]
        public int? ExecutiveNationality { get; set; }
        [DataMember]
        public string ExecutiveBirthPlace { get; set; }
        [DataMember]
        public int? ExecutiveProfession { get; set; }
        [DataMember]
        public AddressInfo ExecutiveResidencyAddress { get; set; }
        [DataMember]
        public AddressInfo CompanyAddress { get; set; }
    }
    [DataContract]
    public class SubscriptionRegistrationInfo
    {
        [DataMember]
        public int? ServiceID { get; set; }
        [DataMember]
        public AddressInfo SetupAddress { get; set; }
        [DataMember]
        public int? BillingPeriod { get; set; }
        //[DataMember]
        //public ReferralDiscountInfo ReferralDiscountInfo { get; set; }
    }
    [DataContract]
    public class ReferralDiscountInfo
    {
        [DataMember]
        public string ReferenceNo { get; set; }
        //public int? SpecialOfferID { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceNewCustomerRegisterRequest : BaseRequest<NewCustomerRegisterRequest, SHA256>
    {
        [DataMember]
        public NewCustomerRegisterRequest CustomerRegisterParameters
        {
            get { return Data; }
            set { Data = value; }
        }

    }
}
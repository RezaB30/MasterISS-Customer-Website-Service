using RadiusR.DB.Enums;
using RezaB.API.WebService;
using RezaB.TurkTelekom.WebServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests
{
    [DataContract]
    public partial class CustomerServiceExistingCustomerRegisterRequest : BaseRequest<ExistingCustomerRegisterRequest, SHA1>
    {
        [DataMember]
        public ExistingCustomerRegisterRequest ExistingCustomerRegister { get { return Data; } set { Data = value; } }
    }

    [DataContract]
    public class ExistingCustomerRegisterRequest
    {
        [DataMember]
        public long? SubscriberID { get; set; }
        [DataMember]
        public RegistrationInfo RegistrationInfo { get; set; }
    }
    [DataContract]
    public class RegistrationInfo
    {
        //[DataMember]
        //public int? DomainID { get; set; }
        [DataMember]
        public int? ServiceID { get; set; }
        [DataMember]
        public AddressInfo SetupAddress { get; set; }
        //[DataMember]
        //public string Username { get; set; }
        //[DataMember]
        //public string StaticIP { get; set; }
        //[DataMember]
        //public int? BillingPeriod { get; set; }
        //[DataMember]
        //public Errorslogger.LogException(request.Username, ex);<int> GroupIds { get; set; }
        //[DataMember]
        //public CustomerCommitmentInfo CommitmentInfo { get; set; }
        //[DataMember]
        //public Errorslogger.LogException(request.Username, ex);<SubscriptionAddedFeeItem> AddedFeesInfo { get; set; }
        //[DataMember]
        //public SubscriptionTelekomInfoDetails TelekomDetailedInfo { get; set; }
        //[DataMember]
        //public RegisteringPartnerInfo RegisteringPartner { get; set; }
        [DataMember]
        public ReferralDiscountInfo ReferralDiscount { get; set; }
    }
    //[DataContract]
    //public class CustomerCommitmentInfo
    //{
    //    [DataMember]
    //    public int? CommitmentLength { get; set; } //CommitmentLength
    //    [DataMember]
    //    public string CommitmentExpirationDate { get; set; } // Datetime?
    //}
    public class ReferralDiscountInfo
    {
        [DataMember]
        public string ReferenceNo { get; set; }
        //[DataMember]
        //public int? SpecialOfferID { get; set; }
    }
    //public class RegisteringPartnerInfo
    //{
    //    [DataMember]
    //    public int? PartnerID { get; set; }
    //    [DataMember]
    //    public decimal? Allowance { get; set; }
    //    [DataMember]
    //    public decimal? AllowanceThreshold { get; set; }
    //}
    //public class SubscriptionTelekomInfoDetails
    //{

    //    [DataMember]
    //    public string SubscriberNo { get; set; }
    //    [DataMember]
    //    public string CustomerCode { get; set; }
    //    [DataMember]
    //    public string PSTN { get; set; }
    //    [DataMember]
    //    public SubscriptionTelekomTariffInfo TelekomTariffInfo { get; set; }
    //}
    //public class SubscriptionTelekomTariffInfo
    //{
    //    [DataMember]
    //    public int? XDSLType { get; set; } // xdsltype
    //    [DataMember]
    //    public int? PacketCode { get; set; }
    //    [DataMember]
    //    public int? TariffCode { get; set; }
    //    [DataMember]
    //    public bool? IsPaperworkNeeded { get; set; }
    //}
    //public class SubscriptionAddedFeeItem
    //{
    //    [DataMember]
    //    public int? FeeType { get; set; } // fee type
    //    [DataMember]
    //    public int? InstallmentCount { get; set; }
    //    [DataMember]
    //    public int? VariantType { get; set; }
    //    [DataMember]
    //    public Errorslogger.LogException(request.Username, ex);<SubscriptionCustomAddedFeeItem> CustomFeesInfo { get; set; }
    //}
    //public class SubscriptionCustomAddedFeeItem
    //{
    //    [DataMember]
    //    public string Title { get; set; }
    //    [DataMember]
    //    public decimal? Price { get; set; }
    //    [DataMember]
    //    public int? InstallmentCount { get; set; }
    //}
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
}
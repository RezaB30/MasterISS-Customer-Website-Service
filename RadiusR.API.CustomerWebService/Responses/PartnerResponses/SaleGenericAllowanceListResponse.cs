using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    public class SaleGenericAllowanceListResponse : PaginationResponse
    {
        [DataMember]
        public SaleGenericAllowanceList[] SaleGenericAllowances { get; set; }
        [DataContract]
        public class SaleGenericAllowanceList
        {
            [DataMember]
            public string SubscriptionNo { get; set; }
            [DataMember]
            public string MembershipDate { get; set; }
            [DataMember]
            public NameValuePair AllowanceState { get; set; }
            [DataMember]
            public NameValuePair SaleState { get; set; }
            [DataMember]
            public decimal? Allowance { get; set; }
        }        
    }
    [DataContract]
    public partial class PartnerServiceSaleGenericAllowanceListResponse : BaseResponse<SaleGenericAllowanceListResponse, SHA256>
    {
        public PartnerServiceSaleGenericAllowanceListResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SaleGenericAllowanceListResponse SaleGenericAllowanceList { get { return Data; } set { Data = value; } }
    }
}
using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    public class SaleAllowanceListResponse : PaginationResponse
    {
        [DataMember]
        public SaleAllowanceList[] SaleAllowances { get; set; }
        [DataContract]
        public class SaleAllowanceList
        {
            [DataMember]
            public long ID { get; set; }
            [DataMember]
            public string IssueDate { get; set; }
            [DataMember]
            public string PaymentDate { get; set; }
            [DataMember]
            public decimal Total { get; set; }
            [DataMember]
            public bool IsPaid { get; set; }
        }        
    }
    [DataContract]
    public partial class PartnerServiceSaleAllowanceListResponse : BaseResponse<SaleAllowanceListResponse, SHA256>
    {
        public PartnerServiceSaleAllowanceListResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SaleAllowanceListResponse SaleAllowanceList { get { return Data; } set { Data = value; } }
    }
}
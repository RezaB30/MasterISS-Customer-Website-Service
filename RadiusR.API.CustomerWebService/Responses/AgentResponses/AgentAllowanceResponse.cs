using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.AgentResponses
{
    [DataContract]
    public class AgentAllowanceResponse : PaginationResponse
    {
        [DataMember]
        public Collection[] Collections { get; set; }
        [DataContract]
        public class Collection
        {
            [DataMember]
            public long CollectionID { get; set; }
            [DataMember]
            public string CompanyTitle { get; set; }
            [DataMember]
            public string CreationDate { get; set; }
            [DataMember]
            public string PaymentDate { get; set; }
            [DataMember]
            public decimal AllowanceAmount { get; set; }
            [DataMember]
            public bool PaymentStatus { get; set; }
        }
    }
    [DataContract]
    public partial class AgentServiceAllowanceResponse : BaseResponse<AgentAllowanceResponse, SHA256>
    {
        public AgentServiceAllowanceResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public AgentAllowanceResponse AgentAllowances { get { return Data; } set { Data = value; } }
    }
}
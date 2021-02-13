using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    [DataContract]
    public class SetupAllowanceListResponse : PaginationResponse
    {
        [DataMember]
        public SetupAllowanceList[] SetupAllowances { get; set; }
        [DataContract]
        public class SetupAllowanceList
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
    public partial class PartnerServiceSetupAllowanceListResponse : BaseResponse<SetupAllowanceListResponse, SHA256>
    {
        public PartnerServiceSetupAllowanceListResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SetupAllowanceListResponse SetupAllowanceList { get { return Data; } set { Data = value; } }
    }
}
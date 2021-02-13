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
    public class SetupGenericAllowanceListResponse : PaginationResponse
    {
        [DataMember]
        public SetupGenericAllowanceList[] SetupGenericAllowances { get; set; }
        [DataContract]
        public class SetupGenericAllowanceList
        {
            [DataMember]
            public string SubscriptionNo { get; set; }
            [DataMember]
            public string IssueDate { get; set; }
            [DataMember]
            public NameValuePair AllowanceState { get; set; }
            [DataMember]
            public string CompletionDate { get; set; }
            [DataMember]
            public NameValuePair SetupState { get; set; }
            [DataMember]
            public decimal? Allowance { get; set; }
        }
    }
    [DataContract]
    public partial class PartnerServiceSetupGenericAllowanceListResponse : BaseResponse<SetupGenericAllowanceListResponse, SHA256>
    {
        public PartnerServiceSetupGenericAllowanceListResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SetupGenericAllowanceListResponse SetupGenericAllowanceList { get { return Data; } set { Data = value; } }
    }
}
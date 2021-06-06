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
    public class RelatedPaymentsResponse : PaginationResponse
    {
        [DataMember]
        public RelatedPayments[] RelatedPaymentList { get; set; }

        [DataContract]
        public class RelatedPayments
        {
            [DataMember]
            public long BillID { get; set; }
            [DataMember]
            public string IssueDate { get; set; }
            [DataMember]
            public string PayDate { get; set; }
            [DataMember]
            public string SubscriberNo { get; set; }
            [DataMember]
            public string ValidDisplayName { get; set; }
            [DataMember]
            public decimal Cost { get; set; }
            [DataMember]
            public string Description { get; set; }
        }

    }
    [DataContract]
    public partial class AgentServiceRelatedPaymentsResponse : BaseResponse<RelatedPaymentsResponse, SHA256>
    {
        public AgentServiceRelatedPaymentsResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public RelatedPaymentsResponse RelatedPayments { get { return Data; } set { Data = value; } }
    }
}
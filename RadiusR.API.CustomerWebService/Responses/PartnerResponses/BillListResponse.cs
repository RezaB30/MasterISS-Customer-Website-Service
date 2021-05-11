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
    public class BillListResponse
    {
        [DataMember]
        public decimal TotalCredits { get; set; }

        [DataMember]
        public string SubscriberName { get; set; }

        [DataMember]
        public BillInfo[] Bills { get; set; }

        [DataContract]
        public class BillInfo
        {
            [DataMember]
            public string SubscriberNo { get; set; }
            [DataMember]
            public string ServiceName { get; set; }
            [DataMember]
            public long ID { get; set; }

            [DataMember]
            public string IssueDate { get; set; }

            [DataMember]
            public string DueDate { get; set; }

            [DataMember]
            public decimal Total { get; set; }
        }
    }
    [DataContract]
    public partial class PartnerServiceBillListResponse : BaseResponse<BillListResponse, SHA256>
    {
        public PartnerServiceBillListResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public BillListResponse BillListResponse { get { return Data; } set { Data = value; } }
    }
}
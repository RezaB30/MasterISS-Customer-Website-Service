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
    public class CreditReportResponse
    {
        [DataMember]
        public decimal Total { get; set; }

        [DataMember]
        public CreditChangeItem[] Details { get; set; }

    }
    [DataContract]
    public class CreditChangeItem
    {
        [DataMember]
        public decimal Amount { get; set; }

        [DataMember]
        public string Date { get; set; }

        [DataMember]
        public string Details { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceCreditReportResponse : BaseResponse<CreditReportResponse, SHA256>
    {
        public PartnerServiceCreditReportResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public CreditReportResponse CreditReportResponse { get { return Data; } set { Data = value; } }
    }
}
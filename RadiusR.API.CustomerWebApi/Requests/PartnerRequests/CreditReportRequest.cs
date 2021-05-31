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
    public class CreditReportRequest : RequestBase
    {
        [DataMember]
        public bool? WithDetails { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceCreditReportRequest : BaseRequest<CreditReportRequest, SHA256>
    {
        [DataMember]
        public CreditReportRequest CreditReportRequest { get { return Data; } set { Data = value; } }
    }
}
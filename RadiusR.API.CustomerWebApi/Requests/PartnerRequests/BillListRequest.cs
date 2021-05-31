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
    public class BillListRequest : RequestBase
    {
        [DataMember]
        public string CustomerCode { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceBillListRequest : BaseRequest<BillListRequest, SHA256>
    {
        [DataMember]
        public BillListRequest BillListRequest { get { return Data; } set { Data = value; } }
    }
}
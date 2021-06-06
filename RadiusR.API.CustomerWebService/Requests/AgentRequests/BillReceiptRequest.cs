using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.AgentRequests
{
    [DataContract]
    public class BillReceiptRequest : RequestBase
    {
        [DataMember]
        public long? BillId { get; set; }

    }
    [DataContract]
    public partial class AgentServiceBillReceiptRequest : BaseRequest<BillReceiptRequest, SHA256>
    {
        public BillReceiptRequest BillReceiptParameters { get { return Data; } set { Data = value; } }
    }
}
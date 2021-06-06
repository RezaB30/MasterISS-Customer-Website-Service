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
    public class AddWorkOrderRequest : RequestBase
    {
        [DataMember]
        public long SubscriptionId { get; set; }
        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public bool? HasModem { get; set; }
        [DataMember]
        public string ModemName { get; set; }
        [DataMember]
        public int SetupUserId { get; set; }
        [DataMember]
        public short TaskType { get; set; }
        [DataMember]
        public short XDSLType { get; set; }

    }
    [DataContract]
    public partial class AgentServiceAddWorkOrderRequest : BaseRequest<AddWorkOrderRequest, SHA256>
    {
        [DataMember]
        public AddWorkOrderRequest AddWorkOrder { get { return Data; } set { Data = value; } }
    }
}
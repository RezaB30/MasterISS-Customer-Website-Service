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
    public class SMSCodeRequest : RequestBase
    {
        [DataMember]
        public string PhoneNo { get; set; }
    }
    [DataContract]
    public partial class AgentServiceSMSCodeRequest : BaseRequest<SMSCodeRequest, SHA256>
    {
        [DataMember]
        public SMSCodeRequest SMSCodeRequest { get { return Data; } set { Data = value; } }
    }
}
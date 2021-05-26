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
    public class ParameterlessRequest : RequestBase { }
    [DataContract]
    public partial class AgentServiceParameterlessRequest : BaseRequest<ParameterlessRequest, SHA256>
    {
        [DataMember]
        public ParameterlessRequest ParameterlessRequest { get { return Data; } set { Data = value; } }
    }
}
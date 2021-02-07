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
    public class ParameterlessRequest : RequestBase { }
    [DataContract]
    public partial class PartnerServiceParameterlessRequest : BaseRequest<ParameterlessRequest, SHA256>
    {
        [DataMember]
        public ParameterlessRequest ParameterlessRequest { get { return Data; } set { Data = value; } }
    }
}
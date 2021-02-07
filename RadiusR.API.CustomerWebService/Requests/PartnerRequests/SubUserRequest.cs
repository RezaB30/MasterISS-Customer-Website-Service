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
    public class SubUserRequest : RequestBase
    {
        [DataMember]
        public string RequestedSubUserEmail { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceSubUserRequest : BaseRequest<SubUserRequest, SHA256>
    {
        [DataMember]
        public SubUserRequest SubUserRequest { get { return Data; } set { Data = value; } }
    }
}
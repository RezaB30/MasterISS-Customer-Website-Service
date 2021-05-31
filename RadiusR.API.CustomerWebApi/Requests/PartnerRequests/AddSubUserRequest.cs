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
    public class AddSubUserRequest : RequestBase
    {
        [DataMember]
        public string RequestedSubUserName { get; set; }

        [DataMember]
        public string RequestedSubUserPassword { get; set; }
        [DataMember]
        public string RequestedSubUserEmail { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceAddSubUserRequest : BaseRequest<AddSubUserRequest, SHA256>
    {
        [DataMember]
        public AddSubUserRequest AddSubUserRequestParameters { get { return Data; } set { Data = value; } }
    }
}
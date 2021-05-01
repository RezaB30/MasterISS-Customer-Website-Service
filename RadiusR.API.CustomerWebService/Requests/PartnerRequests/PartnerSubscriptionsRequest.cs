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
    public partial class PartnerServiceSubscriptionsRequest : BaseRequest<RequestBase, SHA256>
    {
        [DataMember]
        public RequestBase SubscriptionsRequestParameters { get { return Data; } set { Data = value; } }
    }
}
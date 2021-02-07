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
    public class ListFromIDRequest : RequestBase
    {
        public long? ID { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceListFromIDRequest :  BaseRequest<ListFromIDRequest, SHA256>
    {
        [DataMember]
        public ListFromIDRequest ListFromIDRequest { get { return Data; } set { Data = value; } }
    }
}
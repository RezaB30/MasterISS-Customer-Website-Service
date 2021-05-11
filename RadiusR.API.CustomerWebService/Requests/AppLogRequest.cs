using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests
{
    [DataContract]
    public partial class CustomerServiceAppLogRequest : BaseRequest<string,SHA1>
    {
        [DataMember]
        public string LogDescription { get { return Data; } set { Data = value; } }
    }
}
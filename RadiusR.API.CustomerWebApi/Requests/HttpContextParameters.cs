using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests
{
    [DataContract]
    public class HttpContextParameters
    {
        [DataMember]
        public string UserHostAddress { get; set; }
        [DataMember]
        public string UserAgent { get; set; }
    }
}
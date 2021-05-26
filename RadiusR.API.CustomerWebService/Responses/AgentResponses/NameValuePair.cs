using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.AgentResponses
{
    [DataContract]
    public class NameValuePair
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public int Value { get; set; }
    }
}
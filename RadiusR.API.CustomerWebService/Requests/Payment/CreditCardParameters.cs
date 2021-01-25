using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.Payment
{
    [DataContract]
    public class CreditCardParameters
    {
        [DataMember]
        public string CardholderName { get; set; }
        [DataMember]
        public string CardNo { get; set; }
        [DataMember]
        public string ExpirationMonth { get; set; }
        [DataMember]
        public string ExpirationYear { get; set; }
    }
}
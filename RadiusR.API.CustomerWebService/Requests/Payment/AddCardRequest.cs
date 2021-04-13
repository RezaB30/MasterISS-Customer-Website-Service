using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.Payment
{
    [DataContract]
    public class AddCardRequest : CreditCardParameters
    {
        //[DataMember]
        //public string SMSCode { get; set; }
        [DataMember]
        public long? SubscriptionId { get; set; }
        [DataMember]
        public HttpContextParameters HttpContextParameters { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceAddCardRequest : BaseRequest<AddCardRequest, SHA1>
    {
        [DataMember]
        public AddCardRequest AddCardParameters
        {
            get { return Data; }
            set { Data = value; }
        }

    }
}
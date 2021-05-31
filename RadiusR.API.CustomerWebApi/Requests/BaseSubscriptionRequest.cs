using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RadiusR.API.CustomerWebService.Requests
{
    [DataContract]
    public class BaseSubscriptionRequest
    {
        [DataMember]
        public long? SubscriptionId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceBaseRequest : BaseRequest<BaseSubscriptionRequest, SHA1>
    {
        [DataMember]
        public BaseSubscriptionRequest SubscriptionParameters
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}
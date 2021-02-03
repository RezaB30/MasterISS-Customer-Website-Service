using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RadiusR.API.TelekomInfrastructureService
{
    [DataContract]
    public class ServiceAvailabilityRequest
    {
        [DataMember]
        public string bbk { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceServiceAvailabilityRequest : BaseRequest<ServiceAvailabilityRequest, SHA1>
    {
        [DataMember]
        public ServiceAvailabilityRequest ServiceAvailabilityParameters
        {
            get { return Data; }
            set { Data = value; }
        }

    }
}

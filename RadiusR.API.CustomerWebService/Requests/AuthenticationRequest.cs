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
    public class AuthenticationRequest
    {
        [DataMember]
        public string CustomerCode { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceAuthenticationRequest : BaseRequest<AuthenticationRequest, SHA1>
    {
        [DataMember]
        public AuthenticationRequest AuthenticationParameters
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}

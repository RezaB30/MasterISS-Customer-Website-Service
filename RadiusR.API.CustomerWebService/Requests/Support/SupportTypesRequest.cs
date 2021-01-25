using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;


namespace RadiusR.API.CustomerWebService.Requests.Support
{
    [DataContract]
    public class SupportTypesRequest
    {
        [DataMember]
        public bool? IsStaffOnly { get; set; }
        [DataMember]
        public bool? IsDisabled { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceSupportTypesRequest : BaseRequest<SupportTypesRequest, SHA1>
    {
        [DataMember]
        public SupportTypesRequest SupportTypesParameters
        {
            get
            {
                return Data;
            }
            set
            {
                Data = value;
            }
        }
    }
}

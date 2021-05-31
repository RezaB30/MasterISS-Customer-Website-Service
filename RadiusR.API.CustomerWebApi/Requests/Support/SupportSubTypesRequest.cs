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
    public class SupportSubTypesRequest
    {
        [DataMember]
        public int? SupportTypeID { get; set; }
        [DataMember]
        public bool? IsDisabled { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceSupportSubTypesRequest : BaseRequest<SupportSubTypesRequest, SHA1>
    {
        [DataMember]
        public SupportSubTypesRequest SupportSubTypesParameters
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

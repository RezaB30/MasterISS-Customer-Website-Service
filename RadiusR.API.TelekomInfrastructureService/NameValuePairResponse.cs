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
    public class ValueNamePair
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public long? Value { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceNameValuePair : BaseResponse<ValueNamePair[], SHA1>
    {
        public CustomerServiceNameValuePair(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public ValueNamePair[] ValueNamePairList
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}

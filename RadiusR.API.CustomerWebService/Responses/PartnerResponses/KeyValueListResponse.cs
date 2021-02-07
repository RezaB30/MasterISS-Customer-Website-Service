using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    [DataContract]
    public partial class PartnerServiceKeyValueListResponse : BaseResponse<KeyValueItem[],SHA256>
    {
        public PartnerServiceKeyValueListResponse(string passwordHash , BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public KeyValueItem[] KeyValueItemResponse { get; set; }
    }
    [DataContract]
    public class KeyValueItem
    {
        [DataMember]
        public long Key { get; set; }

        [DataMember]
        public string Value { get; set; }
    }
}
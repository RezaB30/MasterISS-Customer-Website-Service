using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.AgentResponses
{
    [DataContract]
    public partial class AgentServiceKeyValueListResponse : BaseResponse<KeyValueItem[],SHA256>
    {
        public AgentServiceKeyValueListResponse(string passwordHash , BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
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
using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.AgentRequests
{
    [DataContract]
    public class AgentAllowanceRequest : RequestBase
    {
        [DataMember]
        public PaginationRequest Pagination { get; set; }
    }
    [DataContract]
    public partial class AgentServiceAllowanceRequest : BaseRequest<AgentAllowanceRequest, SHA256>
    {
        [DataMember]
        public AgentAllowanceRequest AllowanceParameters { get { return Data; } set { Data = value; } }
    }
}
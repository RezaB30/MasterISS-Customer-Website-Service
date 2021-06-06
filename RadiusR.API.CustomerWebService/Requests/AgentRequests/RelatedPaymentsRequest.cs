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
    public class RelatedPaymentsRequest : RequestBase
    {
        [DataMember]
        public PaginationRequest Pagination { get; set; }
    }
    [DataContract]
    public partial class AgentServiceRelatedPaymentsRequest : BaseRequest<RelatedPaymentsRequest, SHA256>
    {
        [DataMember]
        public RelatedPaymentsRequest RelatedPaymentsParameters { get { return Data; } set { Data = value; } }
    }
}
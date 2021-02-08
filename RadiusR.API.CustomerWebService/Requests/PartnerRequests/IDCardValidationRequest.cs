using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.PartnerRequests
{
    [DataContract]
    public class IDCardValidationRequest
    {
        [DataMember]
        public int? IDCardType { get; set; }
        [DataMember]
        public string TCKNo { get; set; }
        [DataMember]
        public string FirstName { get; set; }
        [DataMember]
        public string LastName { get; set; }
        [DataMember]
        public string BirthDate { get; set; }
        [DataMember]
        public string RegistirationNo { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceIDCardValidationRequest : BaseRequest<IDCardValidationRequest, SHA256>
    {
        [DataMember]
        public IDCardValidationRequest IDCardValidationRequest { get { return Data; } set { Data = value; } }
    }
}
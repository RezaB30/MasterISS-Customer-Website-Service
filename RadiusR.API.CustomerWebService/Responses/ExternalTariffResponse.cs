using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public class ExternalTariffResponse
    {
        [DataMember]
        public int TariffID { get; set; }
        //[DataMember]
        //public int DomainID { get; set; }
        [DataMember]
        public string DisplayName { get; set; }
        [DataMember]
        public bool HasXDSL { get; set; }
        [DataMember]
        public bool HasFiber { get; set; }
        [DataMember]
        public decimal Price { get; set; }
        [DataMember]
        public string Speed { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceExternalTariffResponse : BaseResponse<IEnumerable<ExternalTariffResponse>, SHA1>
    {
        public CustomerServiceExternalTariffResponse(string passwordHash, BaseRequest<SHA1> request) : base(passwordHash, request) { }
        [DataMember]
        public IEnumerable<ExternalTariffResponse> ExternalTariffList { get { return Data; } set { Data = value; } }
    }
}
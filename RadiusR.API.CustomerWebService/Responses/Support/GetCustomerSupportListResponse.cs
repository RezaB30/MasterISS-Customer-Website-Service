using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RadiusR.API.CustomerWebService.Responses.Support
{
    [DataContract]
    public class GetCustomerSupportListResponse
    {
        [DataMember]
        public long ID { get; set; }
        [DataMember]
        public string SupportRequestType { get; set; } //  ex: fatura
        [DataMember]
        public string SupportRequestSubType { get; set; } // ex: Faturamı ödeyemiyorum
        [DataMember]
        public string SupportNo { get; set; } // can be id
        [DataMember]
        public string Date { get; set; } // dd.MM.yyyy
        [DataMember]
        public string ApprovalDate { get; set; } // dd.MM.yyyy completed date        
        [DataMember]
        public short State { get; set; } //enum RadiusR.DB.Enums.SupportRequests.SupportRequestStateID
        [DataMember]
        public string StateText { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetCustomerSupportListResponse : BaseResponse<IEnumerable<GetCustomerSupportListResponse>, SHA1>
    {
        public CustomerServiceGetCustomerSupportListResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public IEnumerable<GetCustomerSupportListResponse> GetCustomerSupportListResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}

using RadiusR.API.CustomerWebService.Requests.PartnerRequests;
using RadiusR.API.CustomerWebService.Responses.PartnerResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace RadiusR.API.CustomerWebService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IPartnerService" in both code and config file together.
    [ServiceContract]
    public interface IPartnerService : RadiusR.API.TelekomInfrastructureService.ITelekomInfrastructureService
    {
        [OperationContract]
        string GetKeyFragment(string username);
        [OperationContract]
        PartnerServicePaymentResponse PayBills(PartnerServicePaymentRequest request);
        [OperationContract]
        PartnerServiceAuthenticationResponse Authenticate(PartnerServiceAuthenticationRequest request);
        [OperationContract]
        PartnerServiceAddSubUserResponse AddSubUser(PartnerServiceAddSubUserRequest request);
        [OperationContract]
        PartnerServiceSubUserResponse DisableSubUser(PartnerServiceSubUserRequest request);
        [OperationContract]
        PartnerServiceSubUserResponse EnableSubUser(PartnerServiceSubUserRequest request);
        [OperationContract]
        PartnerServiceBillListResponse BillsBySubscriberNo(PartnerServiceBillListRequest request);
        [OperationContract]
        PartnerServiceKeyValueListResponse GetCultures(PartnerServiceParameterlessRequest request);
        [OperationContract]
        PartnerServiceKeyValueListResponse GetTCKTypes(PartnerServiceParameterlessRequest request);
        [OperationContract]
        PartnerServiceKeyValueListResponse GetCustomerTypes(PartnerServiceParameterlessRequest request);
        [OperationContract]
        PartnerServiceKeyValueListResponse GetSexes(PartnerServiceParameterlessRequest request);
        [OperationContract]
        PartnerServiceKeyValueListResponse GetNationalities(PartnerServiceParameterlessRequest request);
        [OperationContract]
        PartnerServiceKeyValueListResponse GetProfessions(PartnerServiceParameterlessRequest request);
        [OperationContract]
        PartnerServiceKeyValueListResponse GetPartnerTariffs(PartnerServiceParameterlessRequest request);
        [OperationContract]
        PartnerServiceKeyValueListResponse GetPaymentDays(PartnerServiceListFromIDRequest request);
        [OperationContract]
        PartnerServiceCreditReportResponse GetCreditReport(PartnerServiceCreditReportRequest request);
        [OperationContract]
        PartnerServiceSMSCodeResponse SendConfirmationSMS(PartnerServiceSMSCodeRequest request);
        [OperationContract]
        PartnerServiceNewCustomerRegisterResponse NewCustomerRegister(PartnerServiceNewCustomerRegisterRequest request);
        [OperationContract]
        PartnerServiceIDCardValidationResponse IDCardValidation(PartnerServiceIDCardValidationRequest request);
        [OperationContract]
        PartnerServiceAllowanceDetailsResponse GetBasicAllowanceDetails(PartnerServiceBasicAllowanceRequest request);
        [OperationContract]
        PartnerServiceSetupGenericAllowanceListResponse SetupGenericAllowanceList(PartnerServiceAllowanceRequest request);
        [OperationContract]
        PartnerServiceSetupAllowanceListResponse SetupAllowanceList(PartnerServiceAllowanceRequest request);
        [OperationContract]
        PartnerServiceSetupGenericAllowanceListResponse SetupAllowanceDetails(PartnerServiceAllowanceDetailRequest request);
        [OperationContract]
        PartnerServiceSaleAllowanceListResponse SaleAllowanceList(PartnerServiceAllowanceRequest request);
        [OperationContract]
        PartnerServiceSaleGenericAllowanceListResponse SaleAllowanceDetails(PartnerServiceAllowanceDetailRequest request);
        [OperationContract]
        PartnerServiceSaleGenericAllowanceListResponse SaleGenericAllowanceList(PartnerServiceAllowanceRequest request);
        [OperationContract]
        PartnerServiceSubscriptionsResponse GetPartnerSubscriptions(PartnerServiceSubscriptionsRequest request);
        [OperationContract]
        PartnerServiceClientAttachmentsResponse GetPartnerClientAttachments(PartnerServiceClientAttachmentsRequest request);
        [OperationContract]
        PartnerServiceClientFormsResponse GetPartnerClientForms(PartnerServiceClientFormsRequest request);
        [OperationContract]
        PartnerServiceSaveClientAttachmentResponse SaveClientAttachment(PartnerServiceSaveClientAttachmentRequest request);
    }
}

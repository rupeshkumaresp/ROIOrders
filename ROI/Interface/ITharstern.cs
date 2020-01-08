using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ROI.Model;

namespace ROI.Interface
{

    /// <summary>
    /// INTERFACE TO COVERT ROI OBJECT TO THARSTERN ESTIMATE AND JOBS
    /// </summary>
    public interface ITharstern
    {
        Task Authenticate();
        Task CreateJobFromEstimate(string orderId, string documentId, string digiOrLitho);
        Task<string> GetEstRef(string orderId);
        void SetPdfProofProcess(List<TharEstimateRequest.tharProcesses> listProcess, string digiOrLitho);
        Task<List<string>> SubEstimateToTharstern(Order o, string po, Dictionary<string, string> messages, AddressDetails customerAddress);
        string CreatePreDeliveryNoteCustomer(AddressDetails customerAddress, Order o, TharEstimateRequest tharEst, string fileCopies);
        string CreatePreDeliveryNote(Order o, TharEstimateRequest tharEst);
        void SetBindingSide(PrintOptions x, int ProductTypeId, TharEstimateRequest.TharEstParts part);
        void SetFoldingScheme(PrintOptions x, TharEstimateRequest.TharEstParts part);
        void SetFoldingProcess(PrintOptions x, List<TharEstimateRequest.tharProcesses> listPartProcess);
        string GetSpecificPress(PrintOptions x, string digiOrLitho, TharEstimateRequest.TharEstParts part);
        void SetDieCutProcess(PrintOptions x, List<TharEstimateRequest.tharProcesses> listPartProcess);
        void GetQuantity(PrintOptions x, TharEstimateRequest tharEst);
        bool CreateTharsternPushEntry(Order o, PrintOptions x);
        TharEstimateRequest.TharEstCustomer SetCustomer(Order o);
        Task SetBillingAndDeliveryAddress(Order o, TharEstimateRequest tharEst, string documentid, AddressDetails customerAddress);
        void SetCreasingProcessLeaflets(PrintOptions x, List<TharEstimateRequest.tharProcesses> listPartProcess, string digiOrLitho);
        Task SubmitNotes(Order o, string notes);
        bool DownloadPdf(string url, string filename);
        Task SubmitPOandArtwork(Order o, string po, string artwork, string prodName, string documentid);
        void SetOrientation(PrintOptions printOp, TharEstimateRequest tharEst);
        void SetCoatings(string digiOrLitho, PrintOptions x, TharEstimateRequest.TharEstParts part);
        void SetLamination(PrintOptions x, List<TharEstimateRequest.tharProcesses> listPartProcess, string digiOrLitho);
        void SetDrillProcess(List<TharEstimateRequest.tharProcesses> listProcess, string binding);
        void SetColoursAndOtherCoverage(PrintOptions x, TharEstimateRequest.TharEstParts part);
        TharEstimateRequest.tharProcesses SetPackingProcess(PrintOptions printOp);
        void SetSize(TharEstimateRequest.tharEstFinishedSize estFinishedSize, PrintOptions printOp, TharEstimateRequest tharEst);
        string SetJobType(string digiOrLitho);
        string SetDate(string digiOrLitho, DateTime orderDate);
        void DumpTharsternJson(string orderId, string serializedResultJson, string documentid);
        string GetDigiorLithoFromPrintMethod(string orderId, string width, string height, int pageCount, TharEstimateRequest.tharEstFinishedSize estFinishedSize, PrintOptions printOp);
        bool IsValidProductType(List<TharEstimateRequest.TharEstParts> listParts);
        bool IsValidPaperCode(List<TharEstimateRequest.TharEstParts> listParts);
        void UpdateLastMessage(long orderId, string lastMessage, string documentid);
        void AddToLastMessage(long orderId, string lastMessage, string documentid);
        void MarkTharsternPushFailure(string roiorderId);
        void MarkTharsternPushSuccess(string roiorderId, string estRef, string json, string documentId);
        Task<string> CreateTharsternDeliveryAddress(string customerCode);
        Task<string> CreateTharsternCustomerAddress(string customerCode, AddressDetails customerAddress);

        int SetProductTypeId(int pageCount, string orderId, string digiorLitho, string width, string height, PrintOptions printOp);
        int GetProductTypePartId(string sectionName, string printType, string orderId, int prodTypeId, string documentid);
        string GetPaperCode(PrintOptions specification, string orderId, string digiOrLitho, string partType, string documentid);
        string SetGsm(string orderId, string digiOrLitho, string gsm, string paper, string documentid);
        string GetPaper(string stock);
        void UpdateJobDelNote(string documentId, string DelNote);
        void UpdateCustomerDelNote(string documentId, string DelNote);
    }
}
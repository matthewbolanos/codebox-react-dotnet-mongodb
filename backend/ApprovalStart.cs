using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Models;

public class ApprovalStart
{
    public string FormInputId { get; set; }

    public string InstanceId { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; } 

    public string Name { get; set; }

    public string Industry { get; set; }

    public string BusinessProcessDescription { get; set; }

    public string ProcessFrequency { get; set; }

    public string ProcessDuration { get; set; }
}
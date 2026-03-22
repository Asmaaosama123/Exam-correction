namespace ExamCorrection.Contracts.Common;

public class DuplicateCheckSets
{
    public HashSet<string>? EmailSet { get; set; } 
    public HashSet<string>? PhoneSet { get; set; }
    public HashSet<string> FileEmailSet { get; set; } = [];
    public HashSet<string> FilePhoneSet { get; set; } = [];
    public HashSet<string>? NationalIdSet { get; set; }
    public HashSet<string>? FileNationalIds { get; set; }
}
namespace HospitalRecords.Domain.Enums;

public enum ClinicalEntityType
{
    Diagnosis,
    TnmStaging,
    CancerStage,
    HistologicGrade,
    Biomarker,
    ChemotherapyRegimen,
    Medication,
    SurgicalProcedure,
    RadiationTherapy,
    LabFinding
}

public enum MilestoneType
{
    InitialConsult,
    DiagnosticBiopsy,
    PathologyConfirmation,
    DefinitiveSurgery,
    AdjuvantChemotherapy,
    RadiationTherapy,
    RestagingAssessment,
    RemissionOrSurveillance
}

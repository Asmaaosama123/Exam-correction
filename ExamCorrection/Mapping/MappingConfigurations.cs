namespace ExamCorrection.Mapping;

public class MappingConfigurations : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<StudentRequest, Student>();
        config.NewConfig<Class, ClassResponse>()
            .Map(dest => dest.NumberOfStudents, src => src.Students.Count);
    }
}
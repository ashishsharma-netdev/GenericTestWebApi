namespace GenericTestWebApi.Entities;

public class ExamCategoryEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Accent { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public ICollection<MockTestEntity> Tests { get; set; } = new List<MockTestEntity>();
}
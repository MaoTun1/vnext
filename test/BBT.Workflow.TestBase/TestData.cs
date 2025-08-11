using System;

namespace BBT.Workflow;

public class TestData
{
    #region GitRepositories

    public Guid Repository_Id_1 { get; } = Guid.Parse("680f3183-4fb4-4544-bee2-8db56c52f4a6");
    public string Repository_Name_1 { get; } = "Test Repository 1";
    public Guid Repository_Id_2 { get; } = Guid.Parse("701ed8ed-a899-4fc0-94ca-65401e9ad832");
    public string Repository_Name_2 { get; } = "Test Repository 2";

    #endregion

    #region Issues
    public Guid Issue_Id_1 { get; } = Guid.Parse("e96ccaf7-e283-4a00-8730-52cd5a0f96d1");
    public string Issue_Title_1 { get; } = "Test Issue 1";
    
    public Guid Issue_Id_2 { get; } = Guid.Parse("1df6c2ad-51d8-4ca0-853c-12340dd535b2");
    public string Issue_Title_2 { get; } = "Test Issue 2";
    
    public Guid Issue_Id_3 { get; } = Guid.Parse("b5620fc6-0b09-4476-8afd-a72daa2f1107");
    public string Issue_Title_3 { get; } = "Test Issue 3";
    

    #endregion
   
}
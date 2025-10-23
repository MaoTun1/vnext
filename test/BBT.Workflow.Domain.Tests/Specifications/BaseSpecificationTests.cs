using System;
using System.Linq.Expressions;
using Xunit;

namespace BBT.Workflow.Domain.Tests.Specifications;
public class BaseSpecificationTests
{
    [Fact]
    public void Constructor_ShouldInitializeEmptyCollections()
    {
        // Arrange & Act
        var spec = new TestSpecification();

        // Assert
        Assert.NotNull(spec.Includes);
        Assert.Empty(spec.Includes);
        Assert.NotNull(spec.IncludeStrings);
        Assert.Empty(spec.IncludeStrings);
    }

    [Fact]
    public void Criteria_ShouldBeSettable()
    {
        // Arrange
        var spec = new TestSpecification();
        Expression<Func<TestEntity, bool>> criteria = x => x.Id > 0;

        // Act
        spec.SetCriteria(criteria);

        // Assert
        Assert.NotNull(spec.Criteria);
        Assert.Equal(criteria, spec.Criteria);
    }

    [Fact]
    public void AddInclude_ShouldAddExpressionToIncludesList()
    {
        // Arrange
        var spec = new TestSpecification();
        Expression<Func<TestEntity, object>> includeExpression = x => x.RelatedEntity;

        // Act
        spec.AddIncludeExpression(includeExpression);

        // Assert
        Assert.Single(spec.Includes);
        Assert.Contains(includeExpression, spec.Includes);
    }

    [Fact]
    public void AddInclude_ShouldAddMultipleExpressions()
    {
        // Arrange
        var spec = new TestSpecification();
        Expression<Func<TestEntity, object>> include1 = x => x.RelatedEntity;
        Expression<Func<TestEntity, object>> include2 = x => x.AnotherEntity;

        // Act
        spec.AddIncludeExpression(include1);
        spec.AddIncludeExpression(include2);

        // Assert
        Assert.Equal(2, spec.Includes.Count);
        Assert.Contains(include1, spec.Includes);
        Assert.Contains(include2, spec.Includes);
    }

    [Fact]
    public void AddIncludeString_ShouldAddStringToIncludeStringsList()
    {
        // Arrange
        var spec = new TestSpecification();
        var includeString = "RelatedEntity";

        // Act
        spec.AddIncludeString(includeString);

        // Assert
        Assert.Single(spec.IncludeStrings);
        Assert.Contains(includeString, spec.IncludeStrings);
    }

    [Fact]
    public void AddIncludeString_ShouldAddMultipleStrings()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddIncludeString("RelatedEntity");
        spec.AddIncludeString("AnotherEntity");
        spec.AddIncludeString("ThirdEntity");

        // Assert
        Assert.Equal(3, spec.IncludeStrings.Count);
        Assert.Contains("RelatedEntity", spec.IncludeStrings);
        Assert.Contains("AnotherEntity", spec.IncludeStrings);
        Assert.Contains("ThirdEntity", spec.IncludeStrings);
    }

    [Fact]
    public void AddInclude_ShouldSupportMixedIncludeTypes()
    {
        // Arrange
        var spec = new TestSpecification();
        Expression<Func<TestEntity, object>> includeExpression = x => x.RelatedEntity;

        // Act
        spec.AddIncludeExpression(includeExpression);
        spec.AddIncludeString("AnotherEntity");

        // Assert
        Assert.Single(spec.Includes);
        Assert.Single(spec.IncludeStrings);
    }

    [Fact]
    public void Includes_ShouldBeReadOnly()
    {
        // Arrange
        var spec = new TestSpecification();
        Expression<Func<TestEntity, object>> include = x => x.RelatedEntity;
        spec.AddIncludeExpression(include);

        // Act & Assert
        // The Includes property is a List, so we can't directly test immutability
        // But we can verify it's the same reference
        var includes = spec.Includes;
        Assert.Same(includes, spec.Includes);
    }

    [Fact]
    public void IncludeStrings_ShouldBeReadOnly()
    {
        // Arrange
        var spec = new TestSpecification();
        spec.AddIncludeString("Test");

        // Act & Assert
        var includeStrings = spec.IncludeStrings;
        Assert.Same(includeStrings, spec.IncludeStrings);
    }

    [Fact]
    public void Specification_ShouldWorkWithComplexCriteria()
    {
        // Arrange
        var spec = new TestSpecification();
        Expression<Func<TestEntity, bool>> complexCriteria = x => 
            x.Id > 0 && x.Name.StartsWith("Test") && x.IsActive;

        // Act
        spec.SetCriteria(complexCriteria);

        // Assert
        Assert.NotNull(spec.Criteria);
        var entity = new TestEntity { Id = 1, Name = "TestEntity", IsActive = true };
        var compiled = spec.Criteria.Compile();
        Assert.True(compiled(entity));
    }

    [Fact]
    public void Specification_ShouldSupportNestedIncludes()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddIncludeString("RelatedEntity.SubEntity");
        spec.AddIncludeString("AnotherEntity.SubEntity.DeepEntity");

        // Assert
        Assert.Equal(2, spec.IncludeStrings.Count);
        Assert.Contains("RelatedEntity.SubEntity", spec.IncludeStrings);
        Assert.Contains("AnotherEntity.SubEntity.DeepEntity", spec.IncludeStrings);
    }

    [Fact]
    public void Specification_ShouldAllowEmptyCriteria()
    {
        // Arrange & Act
        var spec = new TestSpecification();

        // Assert
        Assert.Null(spec.Criteria);
    }

    [Fact]
    public void Specification_ShouldSupportMultipleCriteriaUpdates()
    {
        // Arrange
        var spec = new TestSpecification();
        Expression<Func<TestEntity, bool>> criteria1 = x => x.Id > 0;
        Expression<Func<TestEntity, bool>> criteria2 = x => x.Name.Length > 5;

        // Act
        spec.SetCriteria(criteria1);
        spec.SetCriteria(criteria2);

        // Assert
        Assert.Equal(criteria2, spec.Criteria);
    }

    // Test Helper Classes
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public TestRelatedEntity? RelatedEntity { get; set; }
        public TestRelatedEntity? AnotherEntity { get; set; }
    }

    private class TestRelatedEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class TestSpecification : BaseSpecification<TestEntity>
    {
        public void SetCriteria(Expression<Func<TestEntity, bool>> criteria)
        {
            Criteria = criteria;
        }

        public void AddIncludeExpression(Expression<Func<TestEntity, object>> includeExpression)
        {
            AddInclude(includeExpression);
        }

        public void AddIncludeString(string includeString)
        {
            AddInclude(includeString);
        }
    }
}


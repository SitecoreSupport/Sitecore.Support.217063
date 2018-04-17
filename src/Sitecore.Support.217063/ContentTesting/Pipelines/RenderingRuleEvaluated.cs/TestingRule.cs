namespace Sitecore.Support.ContentTesting.Pipelines.RenderingRuleEvaluated
{
    using Sitecore.Analytics.Model;
    using Sitecore.Analytics.Pipelines.RenderingRuleEvaluated;
    using Sitecore.ContentTesting;
    using Sitecore.ContentTesting.Model.Data.Items;
    using Sitecore.ContentTesting.Models;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Rules;
    using Sitecore.Rules.ConditionalRenderings;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;

    public class TestingRule : RenderingRuleEvaluatedProcessor
    {
        [SuppressMessage("Microsoft.Security", "CA2104: Do not declare read only mutable reference types", Justification = "readonly makes the factory immutable.")]
        protected readonly IContentTestingFactory factory;

        public TestingRule() : this(null)
        {
        }

        public TestingRule(IContentTestingFactory factory)
        {
            this.factory = (factory ?? ContentTestingFactory.Instance);
        }

        public override void Process(RenderingRuleEvaluatedArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (Context.PageMode.IsExperienceEditor)
            {
                return;
            }
            ID ruleSetId = new ID(args.RuleContext.Reference.UniqueId);
            ID uniqueId = args.Rule.UniqueId;
            PersonalizationRuleData personalizationRuleData = new PersonalizationRuleData(ruleSetId, uniqueId);
            if (args.RuleContext.IsTesting)
            {
                Item item = args.RuleContext.Item.Database.GetItem(args.RuleContext.TestId);
                TestCombination testCombination = this.factory.TestingTracker.GetTestCombination(item.ID.ToGuid());
                if (args.Rule.Evaluate(args.RuleContext))
                {
                    TestVariable testVariable = testCombination.Testset.Variables.FirstOrDefault((TestVariable x) => x.Id.Equals(Guid.Parse(args.RuleContext.Reference.UniqueId)));
                    if (testVariable != null && !testVariable.Values.Any((TestValue x) => x.Id.Equals(args.Rule.UniqueId.Guid)))
                    {
                        TestValue value = testCombination.GetValue(Guid.Parse(args.RuleContext.Reference.UniqueId));
                        if (value.Id.Equals(ID.Null.Guid))
                        {
                            personalizationRuleData.IsOriginal = value.IsOriginal;
                            this.factory.TestingTracker.ClearMvTest();
                            this.factory.PersonalizationTracker.AddRule(personalizationRuleData);
                            return;
                        }
                    }
                }
                if (item != null && !this.ShouldAllowRule(args.Rule, item, args.RuleContext))
                {
                    args.RuleContext.SkipRule = true;
                    personalizationRuleData = null;
                }
            }
            if (personalizationRuleData != null)
            {
                this.factory.PersonalizationTracker.RemoveAllRulesFromTheSet(ruleSetId);
                this.factory.PersonalizationTracker.AddRule(personalizationRuleData);
            }
        }

        protected virtual bool ShouldAllowRule(Rule<ConditionalRenderingsRuleContext> rule, Item testDefinitionItem, ConditionalRenderingsRuleContext ruleContext)
        {
            TestDefinitionItem testDefinitionItem2 = TestDefinitionItem.Create(testDefinitionItem);
            if (testDefinitionItem2 == null || !testDefinitionItem2.IsRunning)
            {
                return true;
            }
            TestCombination testCombination = this.factory.TestingTracker.GetTestCombination(testDefinitionItem2.ID.Guid);
            if (testCombination == null)
            {
                return true;
            }
            Guid ruleSetId = Guid.Parse(ruleContext.Reference.UniqueId);
            Guid ruleId = rule.UniqueId.ToGuid();
            return this.factory.TestingTracker.IsRuleIsInCombination(testCombination, testDefinitionItem2, ruleSetId, ruleId);
        }
    }
}
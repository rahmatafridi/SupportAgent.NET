using System.Text.Json;
using SupportAgent.Core.BusinessQuery;
using SupportAgent.Core.ExternalData;
using SupportAgent.Infrastructure.BusinessQuery;
using SupportAgent.Infrastructure.ExternalData;

namespace SupportAgent.Tests;

public sealed class Phase142RelationshipQueryTests
{
    static readonly Guid Source = Guid.NewGuid();
    static ExternalFieldMapping F(string physical,string logical,string type="String") => new(){ExternalColumnName=physical,BusinessFieldName=logical,DisplayName=logical,DataType=type,IsQueryable=true};
    static ExternalEntityMapping E(string table,string logical,params ExternalFieldMapping[] fields) => new(){Id=Guid.NewGuid(),ExternalDataSourceId=Source,ExternalSchemaName="legacy",ExternalTableName=table,BusinessEntityName=logical,DisplayName=logical,IsEnabled=true,IsValid=true,Fields=[..fields]};
    static ExternalRelationshipMapping R(string name,string from,string fromField,string to,string toField,bool enabled=true) => new(){Id=Guid.NewGuid(),ExternalDataSourceId=Source,Name=name,FromEntity=from,FromField=fromField,ToEntity=to,ToField=toField,IsEnabled=enabled};
    static BusinessTableSchema T(ExternalEntityMapping e)=>new(e.BusinessEntityName,e.DisplayName,"",[],e.Fields.Select(f=>new BusinessColumnSchema(f.BusinessFieldName,f.DisplayName,"",f.DataType)).ToList());
    static BusinessRelationshipSchema S(ExternalRelationshipMapping r)=>new(r.FromEntity,r.FromField,r.ToEntity,r.ToField,r.Name,r.Description,[],r.RelationshipType);

    [Fact] public void Named_relationship_compiles_dynamic_join_and_parameterized_related_filter()
    {
        var kits=E("x_kit","Kits",F("kit_id","Id","Integer"),F("owner_ref","OwnerId","Integer"));
        var people=E("x_person","People",F("person_id","Id","Integer"),F("mail_addr","Email"));
        var owner=R("Owner","Kits","OwnerId","People","Id");
        var schema=new BusinessSchema([T(kits),T(people)],[S(owner)]);
        var plan=new BusinessQueryPlan{Entity="Kits",Select=["Id","Owner.Email"],Filters=[new("Owner.Email",BusinessQueryOperator.Equals,JsonSerializer.SerializeToElement("a@example.test"))],Limit=10};
        var query=new SqlServerBusinessQueryCompiler().Compile(plan,schema,[kits,people],[owner]);
        Assert.Contains("LEFT JOIN [legacy].[x_person] [t1] ON [t0].[owner_ref] = [t1].[person_id]",query.CommandText);
        Assert.Contains("[t1].[mail_addr] = @p0",query.CommandText);
        Assert.Equal("a@example.test",query.Parameters.Single().Value);
    }

    [Fact] public void Multi_hop_relationship_and_distinct_aggregation_compile_from_metadata()
    {
        var records=E("a_record","Records",F("record_id","Id","Integer"),F("item_ref","ItemId","Integer"));
        var items=E("b_item","Items",F("item_id","Id","Integer"),F("order_ref","OrderId","Integer"));
        var orders=E("c_order","Orders",F("order_id","Id","Integer"),F("order_no","OrderNumber"));
        var item=R("Item","Records","ItemId","Items","Id");var order=R("Order","Items","OrderId","Orders","Id");
        var schema=new BusinessSchema([T(records),T(items),T(orders)],[S(item),S(order)]);
        var plan=new BusinessQueryPlan{Entity="Records",Select=["Item.Order.OrderNumber"],Aggregations=[new(BusinessAggregationFunction.Count,"Id","RecordCount",true)],Limit=25};
        var query=new SqlServerBusinessQueryCompiler().Compile(plan,schema,[records,items,orders],[item,order]);
        Assert.Contains("LEFT JOIN [legacy].[b_item] [t1]",query.CommandText);
        Assert.Contains("LEFT JOIN [legacy].[c_order] [t2]",query.CommandText);
        Assert.Contains("COUNT(DISTINCT [t0].[record_id])",query.CommandText);
    }

    [Fact] public void Multiple_foreign_keys_to_same_table_require_relationship_name()
    {
        var rows=E("x_row","Rows",F("created_by","CreatedById","Integer"),F("updated_by","UpdatedById","Integer"));
        var users=E("x_user","Users",F("id","Id","Integer"),F("name","Name"));
        var created=R("CreatedBy","Rows","CreatedById","Users","Id");var updated=R("UpdatedBy","Rows","UpdatedById","Users","Id");
        var schema=new BusinessSchema([T(rows),T(users)],[S(created),S(updated)]);
        var valid=new BusinessQueryPlan{Entity="Rows",Select=["CreatedBy.Name","UpdatedBy.Name"],Limit=10};
        var query=new SqlServerBusinessQueryCompiler().Compile(valid,schema,[rows,users],[created,updated]);
        Assert.Contains("[t0].[created_by] = [t1].[id]",query.CommandText);Assert.Contains("[t0].[updated_by] = [t2].[id]",query.CommandText);
        var ambiguous=new BusinessQueryPlan{Entity="Rows",Select=["Users.Name"],Limit=10};
        Assert.Throws<ExternalDataException>(()=>new SqlServerBusinessQueryCompiler().Compile(ambiguous,schema,[rows,users],[created,updated]));
    }

    [Fact] public void Disabled_and_over_depth_relationship_paths_are_rejected()
    {
        var a=E("a","A",F("id","Id"),F("b","BId"));var b=E("b","B",F("id","Id"),F("c","CId"));var c=E("c","C",F("id","Id"),F("d","DId"));var d=E("d","D",F("id","Id"),F("e","EId"));var e=E("e","E",F("id","Id"));
        var ab=R("B","A","BId","B","Id",false);var bc=R("C","B","CId","C","Id");var cd=R("D","C","DId","D","Id");var de=R("E","D","EId","E","Id");
        var schema=new BusinessSchema([T(a),T(b),T(c),T(d),T(e)],[S(ab),S(bc),S(cd),S(de)]);
        Assert.Throws<ExternalDataException>(()=>new SqlServerBusinessQueryCompiler().Compile(new(){Entity="A",Select=["B.Id"],Limit=10},schema,[a,b,c,d,e],[ab,bc,cd,de]));
        var validation=new BusinessQueryValidator(new StaticSchema(schema)).Validate(new(){Entity="A",Select=["B.C.D.E.Id"],Limit=10});
        Assert.False(validation.IsValid);Assert.Contains(validation.Errors,x=>x.Contains("depth",StringComparison.OrdinalIgnoreCase)||x.Contains("Unknown",StringComparison.OrdinalIgnoreCase));
    }

    [Fact] public void Relevant_schema_selector_keeps_relationship_metadata_and_bridge_entities()
    {
        var schema=new BusinessSchema([
            new("Kits","Kits","Laboratory kits",["kit"],[new("OwnerId","Owner","","Integer")]),
            new("People","People","Customer owners",["customer","owner"],[new("Id","ID","","Integer"),new("Email","Email","","String")]),
            new("Orders","Orders","Sales orders",["order"],[new("CustomerId","Customer","","Integer")])],
            [new("Kits","OwnerId","People","Id","Owner","Kit owner",["customer"]),new("People","Id","Orders","CustomerId","Orders","Customer orders",["sales"])]);
        var selected=new BusinessSchemaSelector(new StaticSchema(schema)).Select("show kit owners and their orders");
        Assert.Contains(selected.Entities,x=>x.Name=="Kits");Assert.Contains(selected.Entities,x=>x.Name=="People");Assert.Contains(selected.Entities,x=>x.Name=="Orders");Assert.Equal(2,selected.Relationships.Count);
    }

    [Fact] public void Validator_rejects_row_fields_mixed_with_ungrouped_aggregation()
    {
        var kits=E("x_kit","Kits",F("kit_id","Id","Integer"),F("state","Status","Integer"));
        var plan=new BusinessQueryPlan{Entity="Kits",Select=["Id","Status"],Aggregations=[new(BusinessAggregationFunction.Count,"Id","count")],Limit=10};
        var validation=new BusinessQueryValidator(new StaticSchema(new([T(kits)],[]))).Validate(plan);
        Assert.False(validation.IsValid);Assert.Contains(validation.Errors,x=>x.Contains("groupBy",StringComparison.OrdinalIgnoreCase));
    }

    [Fact] public void Latest_uses_configured_trusted_recency_field_instead_of_model_guess()
    {
        var schema=new BusinessSchema([new("Kits","Kits","",["kit"],[new("Id","ID","","String"),new("Created","Created","","DateTime"),new("Status","Status","","Integer")],"Created")],[]);
        var plan=new BusinessQueryPlan{Entity="Kits",Select=["Id"],Sort=[new("Status",BusinessQuerySortDirection.Descending)],Limit=10};
        ExternalPlanSemantics.ApplyTrustedMetadata("Show the latest 10 kits",plan,schema);
        Assert.Collection(plan.Sort,x=>{Assert.Equal("Created",x.Field);Assert.Equal(BusinessQuerySortDirection.Descending,x.Direction);});
    }

    [Fact] public void Latest_without_configured_recency_field_is_rejected_instead_of_guessed()
    {
        var schema=new BusinessSchema([new("Kits","Kits","",["kit"],[new("Id","ID","","String")])],[]);
        var plan=new BusinessQueryPlan{Entity="Kits",Select=["Id"],Limit=10};
        var error=Assert.Throws<ExternalDataException>(()=>ExternalPlanSemantics.ApplyTrustedMetadata("Show newest kits",plan,schema));
        Assert.Equal("RecencyNotConfigured",error.Code);
    }

    [Fact] public void Sql_like_explanation_is_replaced_with_grounded_result_summary()
    {
        var plan=new BusinessQueryPlan{Entity="Kits",Select=["Id"],Sort=[new("Created",BusinessQuerySortDirection.Descending)],Limit=10};
        var result=new BusinessQueryResult(["Id"],[],10,false,5);
        var answer=ExternalExplanationSafety.SafeAnswer("SELECT * FROM trusted_rows",plan,result,"kits");
        Assert.DoesNotContain("SELECT",answer,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("10 kits records",answer);
        Assert.Contains("Created descending",answer);
    }

    [Fact] public void Owner_name_request_replaces_foreign_key_with_approved_related_name_fields()
    {
        var schema=new BusinessSchema([
            new("Kits","Kits","",["kit"],[new("Id","Kit ID","","String"),new("OwnerId","Owner ID","","Guid")]),
            new("Users","Users","",["person"],[new("Id","ID","","Guid"),new("FirstName","First name","Human first name","String",Concepts:["first name"]),new("LastName","Last name","Human last name","String",Concepts:["last name"])])],
            [new("Kits","OwnerId","Users","Id","Owner","The person who owns this record",["owner","customer"])]);
        var plan=new BusinessQueryPlan{Entity="Kits",Select=["Id","OwnerId","Users.FirstName","Users.LastName"],Limit=10};
        ExternalPlanSemantics.ApplyTrustedMetadata("Show kits with the owner's name",plan,schema);
        Assert.DoesNotContain("OwnerId",plan.Select);
        Assert.DoesNotContain(plan.Select,x=>x.StartsWith("Users.",StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Owner.FirstName",plan.Select);
        Assert.Contains("Owner.LastName",plan.Select);
    }

    [Fact] public void Owner_name_request_does_not_use_other_user_relationships()
    {
        var user=new BusinessTableSchema("Users","Users","",["person"],[new("Id","ID","","Guid"),new("Name","Name","Human name","String")]);
        var schema=new BusinessSchema([new("Kits","Kits","",["kit"],[new("OwnerId","Owner ID","","Guid"),new("CreatedById","Creator ID","","Guid")]),user],[
            new("Kits","OwnerId","Users","Id","Owner","Owner of the record",["owner"]),
            new("Kits","CreatedById","Users","Id","CreatedByUser","User that created the record",["creator","audit"])]);
        var plan=new BusinessQueryPlan{Entity="Kits",Select=["OwnerId"],Limit=10};
        ExternalPlanSemantics.ApplyTrustedMetadata("Show the owner's name",plan,schema);
        Assert.Contains("Owner.Name",plan.Select);
        Assert.DoesNotContain(plan.Select,x=>x.StartsWith("CreatedByUser.",StringComparison.OrdinalIgnoreCase));
    }

    [Fact] public void Owner_name_request_without_approved_name_field_fails_safely()
    {
        var schema=new BusinessSchema([
            new("Kits","Kits","",["kit"],[new("OwnerId","Owner ID","","Guid")]),
            new("Users","Users","",["person"],[new("Id","ID","","Guid")])],
            [new("Kits","OwnerId","Users","Id","Owner","Owner of the record",["owner"])]);
        var plan=new BusinessQueryPlan{Entity="Kits",Select=["OwnerId"],Limit=10};
        var error=Assert.Throws<ExternalDataException>(()=>ExternalPlanSemantics.ApplyTrustedMetadata("Show the owner's name",plan,schema));
        Assert.Equal("RelatedFieldNotConfigured",error.Code);
    }

    [Fact] public void Base_entity_qualified_fields_are_normalized_but_relationship_paths_are_preserved()
    {
        var schema=new BusinessSchema([
            new("kits","Kits","",["kit"],[new("id","ID","","String"),new("status","Status","","Integer"),new("owner_id","Owner ID","","Guid")]),
            new("users","Users","",["person"],[new("id","ID","","Guid"),new("first_name","First name","","String",Concepts:["first name"])])],
            [new("kits","owner_id","users","id","Owner","Owner of record",["owner"])]);
        var plan=new BusinessQueryPlan{Entity="kits",Select=["kits.id","kits.status","Owner.first_name"],Filters=[new("kits.status",BusinessQueryOperator.Equals,JsonSerializer.SerializeToElement(1))],Sort=[new("kits.id",BusinessQuerySortDirection.Descending)],GroupBy=["kits.status"],Limit=10};
        ExternalPlanSemantics.ApplyTrustedMetadata("Show owner first name",plan,schema);
        Assert.Contains("id",plan.Select);Assert.Contains("status",plan.Select);Assert.Contains("Owner.first_name",plan.Select);
        Assert.Equal("status",plan.Filters.Single().Field);Assert.Equal("id",plan.Sort.Single().Field);Assert.Equal("status",plan.GroupBy.Single());
    }

    sealed class StaticSchema(BusinessSchema schema):IBusinessSchemaService { public BusinessSchema GetSchema()=>schema; }
}

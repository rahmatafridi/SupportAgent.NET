import { apiFetch } from './api'
import type { BusinessQueryResponse } from '../types/businessQuery'

export type DataSourceSummary={id:string;name:string;provider:string;enabled:boolean;approvedEntities:number}
export type DataScopeSummary={id:string;name:string;description:string;enabled:boolean;entityCount:number}
export async function getBusinessDataSources():Promise<DataSourceSummary[]>{const r=await apiFetch('/api/data-sources');if(!r.ok)throw new Error('Could not load data sources.');return r.json()}
export async function getDataScopes(sourceId:string):Promise<DataScopeSummary[]>{const r=await apiFetch(`/api/data-sources/${sourceId}/scopes`);if(!r.ok)throw new Error('Could not load data scopes.');return r.json()}
export async function askBusinessQuery(question:string,dataSourceId?:string,scopeId?:string):Promise<BusinessQueryResponse>{
  const response=await apiFetch('/api/business-query',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({question,dataSourceId:dataSourceId||null,scopeId:scopeId||null})})
  if(!response.ok){let message='The business query could not be completed.';try{const body=await response.json() as{error?:string};if(body.error)message=body.error}catch{/* clean fallback */}throw new Error(message)}
  return response.json() as Promise<BusinessQueryResponse>
}

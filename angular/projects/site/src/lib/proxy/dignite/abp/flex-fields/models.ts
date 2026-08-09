import type { FlexFieldQueryOperator } from './flex-field-query-operator.enum';
import type { FlexFieldValueType } from './flex-field-value-type.enum';

export interface FlexFieldQueryCondition {
  fieldId?: string;
  fieldName?: string;
  operator?: FlexFieldQueryOperator;
  value?: string;
  valueType?: FlexFieldValueType;
}

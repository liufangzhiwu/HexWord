export interface PushCopyItem {
  key: string;      // "M1", "A1", "E1", "R1"
  title: string;    // PushTitleZH
  content: string;  // PushTextZH
}

export interface TimeSlotConfig {
  slotId: number;
  hour: number;
  minute: number;
  second: number;
  daysOfWeek: number[];       // 1=周一 ... 7=周日
  copyPool: PushCopyItem[];
  copyIndex: number;
  segment: 'active' | 'churn';
}

export interface RegisteredReminder {
  reminderId: number;
  slotId: number;
  triggerTime: number;
  copyKey: string;
}
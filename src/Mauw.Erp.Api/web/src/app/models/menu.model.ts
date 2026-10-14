export interface MenuItem {
  label: string;
  route?: string;
  icon: string;
  children?: MenuItem[];
  isCollapsed?: boolean;
}

export interface MenuStructure {
  items: MenuItem[];
}

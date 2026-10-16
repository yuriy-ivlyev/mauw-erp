export interface MenuItem {
  label: string;
  route?: string;
  icon: string;
  children?: MenuItem[];
  isCollapsed?: boolean;
  /** Se true, la voce è un separatore/etichetta non cliccabile. */
  isSeparator?: boolean;
}

export interface MenuStructure {
  items: MenuItem[];
}

/** The options of the header every screen of the stack shares: the band of the brand with the title in white. */
export interface HeaderOptions {
  /** Where the title sits. */
  readonly headerTitleAlign: 'center';
  /** Color of the band. */
  readonly headerStyle: { readonly backgroundColor: string };
  /** Color of the back arrow and of the buttons of the header. */
  readonly headerTintColor: string;
  /** Font of the title. */
  readonly headerTitleStyle: { readonly fontFamily: string; readonly fontSize: number; readonly color: string };
  /** No line under the band: it is a color on its own. */
  readonly headerShadowVisible: false;
}

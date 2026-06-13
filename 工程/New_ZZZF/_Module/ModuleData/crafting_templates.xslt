<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">  
  <xsl:output method="xml" indent="yes"/>  
  
  <xsl:template match="@*|node()">  
    <xsl:copy>  
      <xsl:apply-templates select="@*|node()"/>  
    </xsl:copy>  
  </xsl:template>  
  
  <xsl:template match="CraftingTemplate[@id='TwoHandedPolearm']/UsablePieces">  
    <xsl:copy>  
      <!-- 复制原有的UsablePiece元素 -->  
      <xsl:apply-templates select="@*|node()"/>  
      <!-- 添加新的UsablePiece元素 -->  
      <UsablePiece piece_id="spear_handle_2" />  
      <UsablePiece piece_id="spear_handle_9" />  
      <UsablePiece piece_id="spear_handle_10" />  
      <UsablePiece piece_id="spear_handle_11" />  
      <UsablePiece piece_id="spear_handle_12" />  
    </xsl:copy>  
  </xsl:template>  
  <xsl:template match="CraftingTemplate[@id='TwoHandedSword']/UsablePieces">  
    <xsl:copy>  
      <!-- 复制原有的UsablePiece元素 -->  
      <xsl:apply-templates select="@*|node()"/>  
      <!-- 添加新的UsablePiece元素 -->  
			<UsablePiece
				piece_id="axe_craft_1_handle" />
			<UsablePiece
				piece_id="axe_craft_4_handle" />
			<UsablePiece
				piece_id="axe_craft_6_handle" />
			<UsablePiece
				piece_id="axe_craft_7_handle" />
			<UsablePiece
				piece_id="axe_craft_8_handle" />
			<UsablePiece
				piece_id="axe_craft_9_handle" />
			<UsablePiece
				piece_id="axe_craft_10_handle" />
			<UsablePiece
				piece_id="axe_craft_14_handle" />
			<UsablePiece
				piece_id="axe_craft_29_handle" />
			<UsablePiece
				piece_id="axe_craft_30_handle" />
			<UsablePiece
				piece_id="axe_craft_31_handle" />
			<UsablePiece
				piece_id="axe_craft_32_handle" />

    </xsl:copy>  
  </xsl:template>  
  <xsl:template match="CraftingTemplate[@id='TwoHandedAxe']/UsablePieces">  
    <xsl:copy>  
      <!-- 复制原有的UsablePiece元素 -->  
      <xsl:apply-templates select="@*|node()"/>  
      <!-- 添加新的UsablePiece元素 -->  
			<UsablePiece
				piece_id="empire_blade_1" />
			<UsablePiece
				piece_id="empire_blade_2" />
			<UsablePiece
				piece_id="empire_blade_3" />
			<UsablePiece
				piece_id="empire_blade_3_blunt" />
			<UsablePiece
				piece_id="empire_blade_4" />
			<UsablePiece
				piece_id="empire_blade_4_blunt" />
			<UsablePiece
				piece_id="empire_blade_5" />
			<UsablePiece
				piece_id="empire_blade_6" />
			<UsablePiece
				piece_id="empire_blade_6_blunt" />
			<UsablePiece
				piece_id="empire_blade_7" />
			<UsablePiece
				piece_id="sturgian_blade_1" />
			<UsablePiece
				piece_id="sturgian_blade_2" />
			<UsablePiece
				piece_id="sturgian_blade_3" />
			<UsablePiece
				piece_id="sturgian_blade_4" />
			<UsablePiece
				piece_id="sturgian_blade_5" />
			<UsablePiece
				piece_id="sturgian_blade_6" />
			<UsablePiece
				piece_id="sturgian_blade_7" />
			<UsablePiece
				piece_id="sturgian_blade_8" />
			<UsablePiece
				piece_id="sturgian_blade_9" />
			<UsablePiece
				piece_id="sturgian_blade_10" />
			<UsablePiece
				piece_id="sturgian_blade_11" />
			<UsablePiece
				piece_id="sturgian_blade_12" />
			<UsablePiece
				piece_id="sturgian_blade_13" />
			<UsablePiece
				piece_id="sturgian_blade_14" />
			<UsablePiece
				piece_id="sturgian_blade_15" />
			<UsablePiece
				piece_id="sturgian_blade_16" />
			<UsablePiece
				piece_id="khuzait_blade_1" />
			<UsablePiece
				piece_id="khuzait_blade_2" />
			<UsablePiece
				piece_id="khuzait_blade_3" />
			<UsablePiece
				piece_id="khuzait_blade_5" />
			<UsablePiece
				piece_id="khuzait_blade_6" />
			<UsablePiece
				piece_id="khuzait_blade_7" />
			<UsablePiece
				piece_id="khuzait_blade_8" />
			<UsablePiece
				piece_id="khuzait_blade_9" />
			<UsablePiece
				piece_id="vlandian_blade_1" />
			<UsablePiece
				piece_id="vlandian_blade_2" />
			<UsablePiece
				piece_id="vlandian_blade_3" />
			<UsablePiece
				piece_id="vlandian_blade_3_blunt" />
			<UsablePiece
				piece_id="vlandian_blade_4" />
			<UsablePiece
				piece_id="vlandian_blade_5" />
			<UsablePiece
				piece_id="vlandian_blade_6" />
			<UsablePiece
				piece_id="vlandian_blade_7" />
			<UsablePiece
				piece_id="vlandian_blade_8" />
			<UsablePiece
				piece_id="vlandian_blade_9" />
			<UsablePiece
				piece_id="vlandian_blade_10" />
			<UsablePiece
				piece_id="vlandian_blade_11" />
			<UsablePiece
				piece_id="vlandian_blade_12" />
			<UsablePiece
				piece_id="vlandian_blade_13" />
			<UsablePiece
				piece_id="vlandian_blade_14" />
			<UsablePiece
				piece_id="cleaver_blade_1" />
			<UsablePiece
				piece_id="cleaver_blade_2" />
			<UsablePiece
				piece_id="cleaver_blade_3" />
			<UsablePiece
				piece_id="cleaver_blade_4" />
			<UsablePiece
				piece_id="cleaver_blade_5" />
			<UsablePiece
				piece_id="aserai_blade_1" />
			<UsablePiece
				piece_id="aserai_blade_2" />
			<UsablePiece
				piece_id="aserai_blade_3" />
			<UsablePiece
				piece_id="aserai_blade_4" />
			<UsablePiece
				piece_id="aserai_blade_5" />
			<UsablePiece
				piece_id="aserai_blade_7" />
			<UsablePiece
				piece_id="aserai_blade_8" />
			<UsablePiece
				piece_id="aserai_blade_9" />
			<UsablePiece
				piece_id="aserai_blade_11" />
			<UsablePiece
				piece_id="battania_blade_1" />
			<UsablePiece
				piece_id="battania_blade_3" />
			<UsablePiece
				piece_id="battania_blade_3_blunt" />
			<UsablePiece
				piece_id="battania_blade_3_iron" />
			<UsablePiece
				piece_id="battania_blade_3_iron_blunt" />
			<UsablePiece
				piece_id="battania_blade_4" />
			<UsablePiece
				piece_id="battania_blade_5" />
			<UsablePiece
				piece_id="battania_blade_6" />
			<UsablePiece
				piece_id="wood_blade_1" />
			<UsablePiece
				piece_id="empire_grip_7" />
			<UsablePiece
				piece_id="empire_grip_8" />
			<UsablePiece
				piece_id="empire_grip_9" />
			<UsablePiece
				piece_id="empire_grip_10" />
			<UsablePiece
				piece_id="empire_grip_11" />
			<UsablePiece
				piece_id="empire_grip_12" />
			<UsablePiece
				piece_id="empire_grip_13" />
			<UsablePiece
				piece_id="empire_grip_14" />
			<UsablePiece
				piece_id="empire_grip_15" />
			<UsablePiece
				piece_id="empire_grip_18" />
			<UsablePiece
				piece_id="sturgian_grip_15" />
			<UsablePiece
				piece_id="sturgian_grip_16" />
			<UsablePiece
				piece_id="sturgian_grip_17" />
			<UsablePiece
				piece_id="sturgian_grip_18" />
			<UsablePiece
				piece_id="sturgian_grip_19" />
			<UsablePiece
				piece_id="sturgian_grip_20" />
			<UsablePiece
				piece_id="sturgian_grip_21" />
			<UsablePiece
				piece_id="sturgian_grip_22" />
			<UsablePiece
				piece_id="sturgian_grip_23" />
			<UsablePiece
				piece_id="sturgian_grip_24" />
			<UsablePiece
				piece_id="sturgian_grip_25" />
			<UsablePiece
				piece_id="sturgian_grip_26" />
			<UsablePiece
				piece_id="sturgian_grip_27" />
			<UsablePiece
				piece_id="sturgian_grip_28" />
			<UsablePiece
				piece_id="sturgian_grip_29" />
			<UsablePiece
				piece_id="sturgian_grip_30" />
			<UsablePiece
				piece_id="sturgian_grip_31" />
			<UsablePiece
				piece_id="sturgian_grip_32" />
			<UsablePiece
				piece_id="sturgian_grip_33" />
			<UsablePiece
				piece_id="khuzait_grip_9" />
			<UsablePiece
				piece_id="khuzait_grip_10" />
			<UsablePiece
				piece_id="khuzait_grip_11" />
			<UsablePiece
				piece_id="khuzait_grip_12" />
			<UsablePiece
				piece_id="khuzait_grip_13" />
			<UsablePiece
				piece_id="khuzait_grip_14" />
			<UsablePiece
				piece_id="khuzait_grip_15" />
			<UsablePiece
				piece_id="khuzait_grip_16" />
			<UsablePiece
				piece_id="khuzait_grip_17" />
			<UsablePiece
				piece_id="vlandian_grip_7" />
			<UsablePiece
				piece_id="vlandian_grip_8" />
			<UsablePiece
				piece_id="vlandian_grip_9" />
			<UsablePiece
				piece_id="vlandian_grip_10" />
			<UsablePiece
				piece_id="vlandian_grip_11" />
			<UsablePiece
				piece_id="vlandian_grip_12" />
			<UsablePiece
				piece_id="vlandian_grip_13" />
			<UsablePiece
				piece_id="vlandian_grip_14" />
			<UsablePiece
				piece_id="vlandian_grip_15" />
			<UsablePiece
				piece_id="vlandian_grip_16" />
			<UsablePiece
				piece_id="vlandian_grip_17" />
			<UsablePiece
				piece_id="vlandian_grip_18" />
			<UsablePiece
				piece_id="cleaver_grip_4" />
			<UsablePiece
				piece_id="cleaver_grip_6" />
			<UsablePiece
				piece_id="cleaver_grip_7" />
			<UsablePiece
				piece_id="cleaver_grip_8" />
			<UsablePiece
				piece_id="cleaver_grip_10" />
			<UsablePiece
				piece_id="aserai_grip_10" />
			<UsablePiece
				piece_id="aserai_grip_11" />
			<UsablePiece
				piece_id="aserai_grip_12" />
			<UsablePiece
				piece_id="aserai_grip_13" />
			<UsablePiece
				piece_id="aserai_grip_14" />
			<UsablePiece
				piece_id="aserai_grip_15" />
			<UsablePiece
				piece_id="aserai_grip_16" />
			<UsablePiece
				piece_id="aserai_grip_17" />
			<UsablePiece
				piece_id="aserai_grip_18" />
			<UsablePiece
				piece_id="aserai_grip_19" />
			<UsablePiece
				piece_id="aserai_grip_20" />
			<UsablePiece
				piece_id="aserai_grip_21" />
			<UsablePiece
				piece_id="aserai_grip_22" />
			<UsablePiece
				piece_id="aserai_grip_23" />
			<UsablePiece
				piece_id="battania_grip_2" />
			<UsablePiece
				piece_id="battania_grip_5" />
			<UsablePiece
				piece_id="battania_grip_6" />
			<UsablePiece
				piece_id="battania_grip_8" />
			<UsablePiece
				piece_id="battania_grip_9" />
			<UsablePiece
				piece_id="wood_grip_2" />

    </xsl:copy>  
  </xsl:template>  
  <xsl:template match="CraftingTemplate[@id='TwoHandedSword']/WeaponDescriptions">  
    <xsl:copy>  
      <!-- 复制原有的UsablePiece元素 -->  
      <xsl:apply-templates select="@*|node()"/>  
      <!-- 添加新的UsablePiece元素 -->  
			<WeaponDescription
				id="TwoHandedBladedPolearm" />
			<WeaponDescription
				id="OneHandedBladedPolearm" />

    </xsl:copy>  
  </xsl:template>  

</xsl:stylesheet>
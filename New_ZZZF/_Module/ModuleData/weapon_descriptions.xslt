<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">  
  <xsl:output method="xml" indent="yes"/>  
  
  <xsl:template match="@*|node()">  
    <xsl:copy>  
      <xsl:apply-templates select="@*|node()"/>  
    </xsl:copy>  
  </xsl:template>  
  
  <xsl:template match="WeaponDescription[@id='OneHandedPolearm']/AvailablePieces">  
    <xsl:copy>  
      <!-- 复制原有的AvailablePiece元素 -->  
      <xsl:apply-templates select="@*|node()"/>  
      <!-- 添加新的AvailablePiece元素 -->  
			<AvailablePiece
				id="spear_handle_2" />
			<AvailablePiece
				id="spear_handle_9" />
			<AvailablePiece
				id="spear_handle_10" />
			<AvailablePiece
				id="spear_handle_11" />
			<AvailablePiece
				id="spear_handle_12" />
    </xsl:copy>  
  </xsl:template>  
    <xsl:template match="WeaponDescription[@id='TwoHandedPolearm']/AvailablePieces">  
    <xsl:copy>  
      <!-- 复制原有的AvailablePiece元素 -->  
      <xsl:apply-templates select="@*|node()"/>  
      <!-- 添加新的AvailablePiece元素 -->  
			<AvailablePiece
				id="spear_handle_2" />
			<AvailablePiece
				id="spear_handle_9" />
			<AvailablePiece
				id="spear_handle_10" />
			<AvailablePiece
				id="spear_handle_11" />
			<AvailablePiece
				id="spear_handle_12" />
    </xsl:copy>  
  </xsl:template>  


    <xsl:template match="WeaponDescription[@id='TwoHandedAxe']/AvailablePieces">  
    <xsl:copy>  
      <!-- 复制原有的AvailablePiece元素 -->  
      <xsl:apply-templates select="@*|node()"/>  
      <!-- 添加新的AvailablePiece元素 -->  
			<AvailablePiece
				id="axe_craft_1_handle" />
			<AvailablePiece
				id="empire_blade_1" />
			<AvailablePiece
				id="empire_blade_2" />
			<AvailablePiece
				id="empire_blade_3" />
			<AvailablePiece
				id="empire_blade_3_blunt" />
			<AvailablePiece
				id="empire_blade_4" />
			<AvailablePiece
				id="empire_blade_4_blunt" />
			<AvailablePiece
				id="empire_blade_5" />
			<AvailablePiece
				id="empire_blade_6" />
			<AvailablePiece
				id="empire_blade_6_blunt" />
			<AvailablePiece
				id="empire_blade_7" />
			<AvailablePiece
				id="sturgian_blade_1" />
			<AvailablePiece
				id="sturgian_blade_2" />
			<AvailablePiece
				id="sturgian_blade_3" />
			<AvailablePiece
				id="sturgian_blade_4" />
			<AvailablePiece
				id="sturgian_blade_5" />
			<AvailablePiece
				id="sturgian_blade_6" />
			<AvailablePiece
				id="sturgian_blade_7" />
			<AvailablePiece
				id="sturgian_blade_8" />
			<AvailablePiece
				id="sturgian_blade_9" />
			<AvailablePiece
				id="sturgian_blade_10" />
			<AvailablePiece
				id="sturgian_blade_11" />
			<AvailablePiece
				id="sturgian_blade_12" />
			<AvailablePiece
				id="sturgian_blade_13" />
			<AvailablePiece
				id="sturgian_blade_14" />
			<AvailablePiece
				id="sturgian_blade_15" />
			<AvailablePiece
				id="sturgian_blade_16" />
			<AvailablePiece
				id="khuzait_blade_1" />
			<AvailablePiece
				id="khuzait_blade_2" />
			<AvailablePiece
				id="khuzait_blade_3" />
			<AvailablePiece
				id="khuzait_blade_5" />
			<AvailablePiece
				id="khuzait_blade_6" />
			<AvailablePiece
				id="khuzait_blade_7" />
			<AvailablePiece
				id="khuzait_blade_8" />
			<AvailablePiece
				id="khuzait_blade_9" />
			<AvailablePiece
				id="vlandian_blade_1" />
			<AvailablePiece
				id="vlandian_blade_2" />
			<AvailablePiece
				id="vlandian_blade_3" />
			<AvailablePiece
				id="vlandian_blade_3_blunt" />
			<AvailablePiece
				id="vlandian_blade_4" />
			<AvailablePiece
				id="vlandian_blade_5" />
			<AvailablePiece
				id="vlandian_blade_6" />
			<AvailablePiece
				id="vlandian_blade_7" />
			<AvailablePiece
				id="vlandian_blade_8" />
			<AvailablePiece
				id="vlandian_blade_9" />
			<AvailablePiece
				id="vlandian_blade_10" />
			<AvailablePiece
				id="vlandian_blade_11" />
			<AvailablePiece
				id="vlandian_blade_12" />
			<AvailablePiece
				id="vlandian_blade_13" />
			<AvailablePiece
				id="vlandian_blade_14" />
			<AvailablePiece
				id="cleaver_blade_1" />
			<AvailablePiece
				id="cleaver_blade_2" />
			<AvailablePiece
				id="cleaver_blade_3" />
			<AvailablePiece
				id="cleaver_blade_4" />
			<AvailablePiece
				id="cleaver_blade_5" />
			<AvailablePiece
				id="aserai_blade_1" />
			<AvailablePiece
				id="aserai_blade_2" />
			<AvailablePiece
				id="aserai_blade_3" />
			<AvailablePiece
				id="aserai_blade_4" />
			<AvailablePiece
				id="aserai_blade_5" />
			<AvailablePiece
				id="aserai_blade_7" />
			<AvailablePiece
				id="aserai_blade_8" />
			<AvailablePiece
				id="aserai_blade_9" />
			<AvailablePiece
				id="aserai_blade_11" />
			<AvailablePiece
				id="battania_blade_1" />
			<AvailablePiece
				id="battania_blade_3" />
			<AvailablePiece
				id="battania_blade_3_blunt" />
			<AvailablePiece
				id="battania_blade_3_iron" />
			<AvailablePiece
				id="battania_blade_3_iron_blunt" />
			<AvailablePiece
				id="battania_blade_4" />
			<AvailablePiece
				id="battania_blade_5" />
			<AvailablePiece
				id="battania_blade_6" />
			<AvailablePiece
				id="wood_blade_1" />
			<AvailablePiece
				id="empire_grip_7" />
			<AvailablePiece
				id="empire_grip_8" />
			<AvailablePiece
				id="empire_grip_9" />
			<AvailablePiece
				id="empire_grip_10" />
			<AvailablePiece
				id="empire_grip_11" />
			<AvailablePiece
				id="empire_grip_12" />
			<AvailablePiece
				id="empire_grip_13" />
			<AvailablePiece
				id="empire_grip_14" />
			<AvailablePiece
				id="empire_grip_15" />
			<AvailablePiece
				id="empire_grip_18" />
			<AvailablePiece
				id="sturgian_grip_15" />
			<AvailablePiece
				id="sturgian_grip_16" />
			<AvailablePiece
				id="sturgian_grip_17" />
			<AvailablePiece
				id="sturgian_grip_18" />
			<AvailablePiece
				id="sturgian_grip_19" />
			<AvailablePiece
				id="sturgian_grip_20" />
			<AvailablePiece
				id="sturgian_grip_21" />
			<AvailablePiece
				id="sturgian_grip_22" />
			<AvailablePiece
				id="sturgian_grip_23" />
			<AvailablePiece
				id="sturgian_grip_24" />
			<AvailablePiece
				id="sturgian_grip_25" />
			<AvailablePiece
				id="sturgian_grip_26" />
			<AvailablePiece
				id="sturgian_grip_27" />
			<AvailablePiece
				id="sturgian_grip_28" />
			<AvailablePiece
				id="sturgian_grip_29" />
			<AvailablePiece
				id="sturgian_grip_30" />
			<AvailablePiece
				id="sturgian_grip_31" />
			<AvailablePiece
				id="sturgian_grip_32" />
			<AvailablePiece
				id="sturgian_grip_33" />
			<AvailablePiece
				id="khuzait_grip_9" />
			<AvailablePiece
				id="khuzait_grip_10" />
			<AvailablePiece
				id="khuzait_grip_11" />
			<AvailablePiece
				id="khuzait_grip_12" />
			<AvailablePiece
				id="khuzait_grip_13" />
			<AvailablePiece
				id="khuzait_grip_14" />
			<AvailablePiece
				id="khuzait_grip_15" />
			<AvailablePiece
				id="khuzait_grip_16" />
			<AvailablePiece
				id="khuzait_grip_17" />
			<AvailablePiece
				id="vlandian_grip_7" />
			<AvailablePiece
				id="vlandian_grip_8" />
			<AvailablePiece
				id="vlandian_grip_9" />
			<AvailablePiece
				id="vlandian_grip_10" />
			<AvailablePiece
				id="vlandian_grip_11" />
			<AvailablePiece
				id="vlandian_grip_12" />
			<AvailablePiece
				id="vlandian_grip_13" />
			<AvailablePiece
				id="vlandian_grip_14" />
			<AvailablePiece
				id="vlandian_grip_15" />
			<AvailablePiece
				id="vlandian_grip_16" />
			<AvailablePiece
				id="vlandian_grip_17" />
			<AvailablePiece
				id="vlandian_grip_18" />
			<AvailablePiece
				id="cleaver_grip_4" />
			<AvailablePiece
				id="cleaver_grip_6" />
			<AvailablePiece
				id="cleaver_grip_7" />
			<AvailablePiece
				id="cleaver_grip_8" />
			<AvailablePiece
				id="cleaver_grip_10" />
			<AvailablePiece
				id="aserai_grip_10" />
			<AvailablePiece
				id="aserai_grip_11" />
			<AvailablePiece
				id="aserai_grip_12" />
			<AvailablePiece
				id="aserai_grip_13" />
			<AvailablePiece
				id="aserai_grip_14" />
			<AvailablePiece
				id="aserai_grip_15" />
			<AvailablePiece
				id="aserai_grip_16" />
			<AvailablePiece
				id="aserai_grip_17" />
			<AvailablePiece
				id="aserai_grip_18" />
			<AvailablePiece
				id="aserai_grip_19" />
			<AvailablePiece
				id="aserai_grip_20" />
			<AvailablePiece
				id="aserai_grip_21" />
			<AvailablePiece
				id="aserai_grip_22" />
			<AvailablePiece
				id="aserai_grip_23" />
			<AvailablePiece
				id="battania_grip_2" />
			<AvailablePiece
				id="battania_grip_5" />
			<AvailablePiece
				id="battania_grip_6" />
			<AvailablePiece
				id="battania_grip_8" />
			<AvailablePiece
				id="battania_grip_9" />
			<AvailablePiece
				id="wood_grip_2" />

    </xsl:copy>  
    </xsl:template> 
    <xsl:template match="WeaponDescription[@id='OneHandedBastardSword']/@weapon_class">  
        <xsl:attribute name="weapon_class">TwoHandedSword</xsl:attribute>  
    </xsl:template> 
    
  <xsl:template match="WeaponDescriptions">  
    <xsl:copy>  
      <!-- 复制原有的AvailablePiece元素 -->  
      <xsl:apply-templates select="@*|node()"/>  
      <!-- 添加新的AvailablePiece元素 -->  
	<WeaponDescription
		id="TwoHandedBladedPolearm"
		weapon_class="TwoHandedSword"
		item_usage_features="twoHandedBladedPolearm">
		<WeaponFlags>
			<WeaponFlag
				value="MeleeWeapon" />
			<WeaponFlag
				value="NotUsableWithOneHand" />
		</WeaponFlags>
		<AvailablePieces>
			<AvailablePiece
				id="empire_blade_1" />
			<AvailablePiece
				id="empire_blade_2" />
			<AvailablePiece
				id="empire_blade_3" />
			<AvailablePiece
				id="empire_blade_3_blunt" />
			<AvailablePiece
				id="empire_blade_4" />
			<AvailablePiece
				id="empire_blade_4_blunt" />
			<AvailablePiece
				id="empire_blade_5" />
			<AvailablePiece
				id="empire_blade_6" />
			<AvailablePiece
				id="empire_blade_7" />
			<AvailablePiece
				id="sturgian_blade_1" />
			<AvailablePiece
				id="sturgian_blade_2" />
			<AvailablePiece
				id="sturgian_blade_3" />
			<AvailablePiece
				id="sturgian_blade_4" />
			<AvailablePiece
				id="sturgian_blade_5" />
			<AvailablePiece
				id="sturgian_blade_6" />
			<AvailablePiece
				id="sturgian_blade_7" />
			<AvailablePiece
				id="sturgian_blade_8" />
			<AvailablePiece
				id="sturgian_blade_9" />
			<AvailablePiece
				id="sturgian_blade_10" />
			<AvailablePiece
				id="sturgian_blade_11" />
			<AvailablePiece
				id="sturgian_blade_12" />
			<AvailablePiece
				id="sturgian_blade_13" />
			<AvailablePiece
				id="sturgian_blade_14" />
			<AvailablePiece
				id="sturgian_blade_15" />
			<AvailablePiece
				id="sturgian_blade_16" />
			<AvailablePiece
				id="khuzait_blade_1" />
			<AvailablePiece
				id="khuzait_blade_2" />
			<AvailablePiece
				id="khuzait_blade_3" />
			<AvailablePiece
				id="khuzait_blade_5" />
			<AvailablePiece
				id="khuzait_blade_6" />
			<AvailablePiece
				id="khuzait_blade_7" />
			<AvailablePiece
				id="khuzait_blade_8" />
			<AvailablePiece
				id="khuzait_blade_9" />
			<AvailablePiece
				id="vlandian_blade_1" />
			<AvailablePiece
				id="vlandian_blade_2" />
			<AvailablePiece
				id="vlandian_blade_3" />
			<AvailablePiece
				id="vlandian_blade_3_blunt" />
			<AvailablePiece
				id="vlandian_blade_4" />
			<AvailablePiece
				id="vlandian_blade_5" />
			<AvailablePiece
				id="vlandian_blade_6" />
			<AvailablePiece
				id="vlandian_blade_7" />
			<AvailablePiece
				id="vlandian_blade_8" />
			<AvailablePiece
				id="vlandian_blade_9" />
			<AvailablePiece
				id="vlandian_blade_11" />
			<AvailablePiece
				id="vlandian_blade_12" />
			<AvailablePiece
				id="vlandian_blade_13" />
			<AvailablePiece
				id="vlandian_blade_14" />
			<AvailablePiece
				id="cleaver_blade_1" />
			<AvailablePiece
				id="cleaver_blade_2" />
			<AvailablePiece
				id="cleaver_blade_3" />
			<AvailablePiece
				id="cleaver_blade_4" />
			<AvailablePiece
				id="cleaver_blade_5" />
			<AvailablePiece
				id="aserai_blade_1" />
			<AvailablePiece
				id="aserai_blade_2" />
			<AvailablePiece
				id="aserai_blade_3" />
			<AvailablePiece
				id="aserai_blade_4" />
			<AvailablePiece
				id="aserai_blade_5" />
			<AvailablePiece
				id="aserai_blade_7" />
			<AvailablePiece
				id="aserai_blade_8" />
			<AvailablePiece
				id="aserai_blade_9" />
			<AvailablePiece
				id="aserai_blade_11" />
			<AvailablePiece
				id="battania_blade_1" />
			<AvailablePiece
				id="battania_blade_3" />
			<AvailablePiece
				id="battania_blade_3_blunt" />
			<AvailablePiece
				id="battania_blade_3_iron" />
			<AvailablePiece
				id="battania_blade_3_iron_blunt" />
			<AvailablePiece
				id="battania_blade_4" />
			<AvailablePiece
				id="battania_blade_5" />
			<AvailablePiece
				id="battania_blade_6" />
			<AvailablePiece
				id="wood_blade_1" />
			<AvailablePiece
				id="empire_guard_1" />
			<AvailablePiece
				id="empire_guard_2" />
			<AvailablePiece
				id="empire_guard_3" />
			<AvailablePiece
				id="empire_guard_4" />
			<AvailablePiece
				id="empire_guard_5" />
			<AvailablePiece
				id="empire_guard_6" />
			<AvailablePiece
				id="sturgian_guard_1" />
			<AvailablePiece
				id="sturgian_guard_2" />
			<AvailablePiece
				id="sturgian_guard_3" />
			<AvailablePiece
				id="sturgian_guard_4" />
			<AvailablePiece
				id="sturgian_guard_5" />
			<AvailablePiece
				id="sturgian_guard_6" />
			<AvailablePiece
				id="sturgian_guard_7" />
			<AvailablePiece
				id="sturgian_guard_8" />
			<AvailablePiece
				id="sturgian_guard_9" />
			<AvailablePiece
				id="sturgian_guard_10" />
			<AvailablePiece
				id="sturgian_guard_11" />
			<AvailablePiece
				id="sturgian_guard_12" />
			<AvailablePiece
				id="sturgian_noble_guard_1" />
			<AvailablePiece
				id="sturgian_noble_guard_2" />
			<AvailablePiece
				id="khuzait_guard_1" />
			<AvailablePiece
				id="khuzait_guard_2" />
			<AvailablePiece
				id="khuzait_guard_3" />
			<AvailablePiece
				id="khuzait_guard_4" />
			<AvailablePiece
				id="khuzait_guard_5" />
			<AvailablePiece
				id="khuzait_guard_6" />
			<AvailablePiece
				id="khuzait_guard_7" />
			<AvailablePiece
				id="khuzait_guard_8" />
			<AvailablePiece
				id="khuzait_guard_9" />
			<AvailablePiece
				id="vlandian_guard_1" />
			<AvailablePiece
				id="vlandian_guard_2" />
			<AvailablePiece
				id="vlandian_guard_3" />
			<AvailablePiece
				id="vlandian_guard_4" />
			<AvailablePiece
				id="vlandian_guard_5" />
			<AvailablePiece
				id="vlandian_guard_6" />
			<AvailablePiece
				id="vlandian_guard_7" />
			<AvailablePiece
				id="vlandian_guard_8" />
			<AvailablePiece
				id="cleaver_guard_1" />
			<AvailablePiece
				id="cleaver_guard_2" />
			<AvailablePiece
				id="cleaver_guard_3" />
			<AvailablePiece
				id="cleaver_guard_4" />
			<AvailablePiece
				id="cleaver_guard_5" />
			<AvailablePiece
				id="aserai_guard_1" />
			<AvailablePiece
				id="aserai_guard_2" />
			<AvailablePiece
				id="aserai_guard_3" />
			<AvailablePiece
				id="aserai_guard_4" />
			<AvailablePiece
				id="aserai_guard_5" />
			<AvailablePiece
				id="aserai_guard_6" />
			<AvailablePiece
				id="aserai_guard_7" />
			<AvailablePiece
				id="aserai_guard_8" />
			<AvailablePiece
				id="aserai_guard_9" />
			<AvailablePiece
				id="battania_guard_1" />
			<AvailablePiece
				id="battania_guard_3" />
			<AvailablePiece
				id="battania_guard_5" />
			<AvailablePiece
				id="battania_guard_6" />
			<AvailablePiece
				id="battania_guard_7" />
			<AvailablePiece
				id="battania_guard_8" />
			<AvailablePiece
				id="battania_guard_9" />
			<AvailablePiece
				id="wood_guard_1" />
			<AvailablePiece
				id="axe_craft_1_handle" />
			<AvailablePiece
				id="axe_craft_4_handle" />
			<AvailablePiece
				id="axe_craft_6_handle" />
			<AvailablePiece
				id="axe_craft_7_handle" />
			<AvailablePiece
				id="axe_craft_8_handle" />
			<AvailablePiece
				id="axe_craft_9_handle" />
			<AvailablePiece
				id="axe_craft_10_handle" />
			<AvailablePiece
				id="axe_craft_14_handle" />
			<AvailablePiece
				id="axe_craft_29_handle" />
			<AvailablePiece
				id="axe_craft_30_handle" />
			<AvailablePiece
				id="axe_craft_31_handle" />
			<AvailablePiece
				id="axe_craft_32_handle" />
			<AvailablePiece
				id="empire_pommel_1" />
			<AvailablePiece
				id="empire_pommel_2" />
			<AvailablePiece
				id="empire_pommel_3" />
			<AvailablePiece
				id="empire_pommel_4" />
			<AvailablePiece
				id="empire_pommel_5" />
			<AvailablePiece
				id="empire_pommel_6" />
			<AvailablePiece
				id="sturgian_pommel_1" />
			<AvailablePiece
				id="sturgian_pommel_2" />
			<AvailablePiece
				id="sturgian_pommel_3" />
			<AvailablePiece
				id="sturgian_pommel_4" />
			<AvailablePiece
				id="sturgian_pommel_5" />
			<AvailablePiece
				id="sturgian_pommel_6" />
			<AvailablePiece
				id="sturgian_pommel_7" />
			<AvailablePiece
				id="sturgian_pommel_8" />
			<AvailablePiece
				id="sturgian_pommel_9" />
			<AvailablePiece
				id="sturgian_pommel_10" />
			<AvailablePiece
				id="sturgian_pommel_11" />
			<AvailablePiece
				id="sturgian_pommel_12" />
			<AvailablePiece
				id="khuzait_pommel_1" />
			<AvailablePiece
				id="khuzait_pommel_2" />
			<AvailablePiece
				id="khuzait_pommel_3" />
			<AvailablePiece
				id="khuzait_pommel_4" />
			<AvailablePiece
				id="khuzait_pommel_5" />
			<AvailablePiece
				id="khuzait_pommel_6" />
			<AvailablePiece
				id="khuzait_pommel_7" />
			<AvailablePiece
				id="vlandian_pommel_1" />
			<AvailablePiece
				id="vlandian_pommel_2" />
			<AvailablePiece
				id="vlandian_pommel_3" />
			<AvailablePiece
				id="vlandian_pommel_4" />
			<AvailablePiece
				id="vlandian_pommel_5" />
			<AvailablePiece
				id="vlandian_pommel_6" />
			<AvailablePiece
				id="vlandian_pommel_7" />
			<AvailablePiece
				id="vlandian_pommel_8" />
			<AvailablePiece
				id="vlandian_pommel_9" />
			<AvailablePiece
				id="vlandian_pommel_10" />
			<AvailablePiece
				id="cleaver_pommel_1" />
			<AvailablePiece
				id="cleaver_pommel_2" />
			<AvailablePiece
				id="cleaver_pommel_3" />
			<AvailablePiece
				id="cleaver_pommel_4" />
			<AvailablePiece
				id="aserai_pommel_1" />
			<AvailablePiece
				id="aserai_pommel_2" />
			<AvailablePiece
				id="aserai_pommel_3" />
			<AvailablePiece
				id="aserai_pommel_4" />
			<AvailablePiece
				id="aserai_pommel_5" />
			<AvailablePiece
				id="aserai_pommel_6" />
			<AvailablePiece
				id="aserai_pommel_8" />
			<AvailablePiece
				id="aserai_pommel_9" />
			<AvailablePiece
				id="battania_pommel_1" />
			<AvailablePiece
				id="battania_pommel_2" />
			<AvailablePiece
				id="battania_pommel_3" />
			<AvailablePiece
				id="battania_pommel_4" />
			<AvailablePiece
				id="battania_pommel_5" />
			<AvailablePiece
				id="battania_pommel_6" />
			<AvailablePiece
				id="battania_pommel_7" />
			<AvailablePiece
				id="battania_pommel_8" />
			<AvailablePiece
				id="battania_pommel_9" />
			<AvailablePiece
				id="wood_pommel_1" />
			<!-- AvailablePieces,TwoHandedSword -->
		</AvailablePieces>
  </WeaponDescription>
	<WeaponDescription
		id="OneHandedBladedPolearm"
		weapon_class="TwoHandedSword"
		item_usage_features="onehanded_shield_swing">
		<WeaponFlags>
			<WeaponFlag
				value="MeleeWeapon" />
		</WeaponFlags>
		<AvailablePieces>
			<AvailablePiece
				id="empire_blade_1" />
			<AvailablePiece
				id="empire_blade_2" />
			<AvailablePiece
				id="empire_blade_3" />
			<AvailablePiece
				id="empire_blade_3_blunt" />
			<AvailablePiece
				id="empire_blade_4" />
			<AvailablePiece
				id="empire_blade_4_blunt" />
			<AvailablePiece
				id="empire_blade_5" />
			<AvailablePiece
				id="empire_blade_6" />
			<AvailablePiece
				id="empire_blade_7" />
			<AvailablePiece
				id="sturgian_blade_1" />
			<AvailablePiece
				id="sturgian_blade_2" />
			<AvailablePiece
				id="sturgian_blade_3" />
			<AvailablePiece
				id="sturgian_blade_4" />
			<AvailablePiece
				id="sturgian_blade_5" />
			<AvailablePiece
				id="sturgian_blade_6" />
			<AvailablePiece
				id="sturgian_blade_7" />
			<AvailablePiece
				id="sturgian_blade_8" />
			<AvailablePiece
				id="sturgian_blade_9" />
			<AvailablePiece
				id="sturgian_blade_10" />
			<AvailablePiece
				id="sturgian_blade_11" />
			<AvailablePiece
				id="sturgian_blade_12" />
			<AvailablePiece
				id="sturgian_blade_13" />
			<AvailablePiece
				id="sturgian_blade_14" />
			<AvailablePiece
				id="sturgian_blade_15" />
			<AvailablePiece
				id="sturgian_blade_16" />
			<AvailablePiece
				id="khuzait_blade_1" />
			<AvailablePiece
				id="khuzait_blade_2" />
			<AvailablePiece
				id="khuzait_blade_3" />
			<AvailablePiece
				id="khuzait_blade_5" />
			<AvailablePiece
				id="khuzait_blade_6" />
			<AvailablePiece
				id="khuzait_blade_7" />
			<AvailablePiece
				id="khuzait_blade_8" />
			<AvailablePiece
				id="khuzait_blade_9" />
			<AvailablePiece
				id="vlandian_blade_1" />
			<AvailablePiece
				id="vlandian_blade_2" />
			<AvailablePiece
				id="vlandian_blade_3" />
			<AvailablePiece
				id="vlandian_blade_3_blunt" />
			<AvailablePiece
				id="vlandian_blade_4" />
			<AvailablePiece
				id="vlandian_blade_5" />
			<AvailablePiece
				id="vlandian_blade_6" />
			<AvailablePiece
				id="vlandian_blade_7" />
			<AvailablePiece
				id="vlandian_blade_8" />
			<AvailablePiece
				id="vlandian_blade_9" />
			<AvailablePiece
				id="vlandian_blade_11" />
			<AvailablePiece
				id="vlandian_blade_12" />
			<AvailablePiece
				id="vlandian_blade_13" />
			<AvailablePiece
				id="vlandian_blade_14" />
			<AvailablePiece
				id="cleaver_blade_1" />
			<AvailablePiece
				id="cleaver_blade_2" />
			<AvailablePiece
				id="cleaver_blade_3" />
			<AvailablePiece
				id="cleaver_blade_4" />
			<AvailablePiece
				id="cleaver_blade_5" />
			<AvailablePiece
				id="aserai_blade_1" />
			<AvailablePiece
				id="aserai_blade_2" />
			<AvailablePiece
				id="aserai_blade_3" />
			<AvailablePiece
				id="aserai_blade_4" />
			<AvailablePiece
				id="aserai_blade_5" />
			<AvailablePiece
				id="aserai_blade_7" />
			<AvailablePiece
				id="aserai_blade_8" />
			<AvailablePiece
				id="aserai_blade_9" />
			<AvailablePiece
				id="aserai_blade_11" />
			<AvailablePiece
				id="battania_blade_1" />
			<AvailablePiece
				id="battania_blade_3" />
			<AvailablePiece
				id="battania_blade_3_blunt" />
			<AvailablePiece
				id="battania_blade_3_iron" />
			<AvailablePiece
				id="battania_blade_3_iron_blunt" />
			<AvailablePiece
				id="battania_blade_4" />
			<AvailablePiece
				id="battania_blade_5" />
			<AvailablePiece
				id="battania_blade_6" />
			<AvailablePiece
				id="wood_blade_1" />
			<AvailablePiece
				id="empire_guard_1" />
			<AvailablePiece
				id="empire_guard_2" />
			<AvailablePiece
				id="empire_guard_3" />
			<AvailablePiece
				id="empire_guard_4" />
			<AvailablePiece
				id="empire_guard_5" />
			<AvailablePiece
				id="empire_guard_6" />
			<AvailablePiece
				id="sturgian_guard_1" />
			<AvailablePiece
				id="sturgian_guard_2" />
			<AvailablePiece
				id="sturgian_guard_3" />
			<AvailablePiece
				id="sturgian_guard_4" />
			<AvailablePiece
				id="sturgian_guard_5" />
			<AvailablePiece
				id="sturgian_guard_6" />
			<AvailablePiece
				id="sturgian_guard_7" />
			<AvailablePiece
				id="sturgian_guard_8" />
			<AvailablePiece
				id="sturgian_guard_9" />
			<AvailablePiece
				id="sturgian_guard_10" />
			<AvailablePiece
				id="sturgian_guard_11" />
			<AvailablePiece
				id="sturgian_guard_12" />
			<AvailablePiece
				id="sturgian_noble_guard_1" />
			<AvailablePiece
				id="sturgian_noble_guard_2" />
			<AvailablePiece
				id="khuzait_guard_1" />
			<AvailablePiece
				id="khuzait_guard_2" />
			<AvailablePiece
				id="khuzait_guard_3" />
			<AvailablePiece
				id="khuzait_guard_4" />
			<AvailablePiece
				id="khuzait_guard_5" />
			<AvailablePiece
				id="khuzait_guard_6" />
			<AvailablePiece
				id="khuzait_guard_7" />
			<AvailablePiece
				id="khuzait_guard_8" />
			<AvailablePiece
				id="khuzait_guard_9" />
			<AvailablePiece
				id="vlandian_guard_1" />
			<AvailablePiece
				id="vlandian_guard_2" />
			<AvailablePiece
				id="vlandian_guard_3" />
			<AvailablePiece
				id="vlandian_guard_4" />
			<AvailablePiece
				id="vlandian_guard_5" />
			<AvailablePiece
				id="vlandian_guard_6" />
			<AvailablePiece
				id="vlandian_guard_7" />
			<AvailablePiece
				id="vlandian_guard_8" />
			<AvailablePiece
				id="cleaver_guard_1" />
			<AvailablePiece
				id="cleaver_guard_2" />
			<AvailablePiece
				id="cleaver_guard_3" />
			<AvailablePiece
				id="cleaver_guard_4" />
			<AvailablePiece
				id="cleaver_guard_5" />
			<AvailablePiece
				id="aserai_guard_1" />
			<AvailablePiece
				id="aserai_guard_2" />
			<AvailablePiece
				id="aserai_guard_3" />
			<AvailablePiece
				id="aserai_guard_4" />
			<AvailablePiece
				id="aserai_guard_5" />
			<AvailablePiece
				id="aserai_guard_6" />
			<AvailablePiece
				id="aserai_guard_7" />
			<AvailablePiece
				id="aserai_guard_8" />
			<AvailablePiece
				id="aserai_guard_9" />
			<AvailablePiece
				id="battania_guard_1" />
			<AvailablePiece
				id="battania_guard_3" />
			<AvailablePiece
				id="battania_guard_5" />
			<AvailablePiece
				id="battania_guard_6" />
			<AvailablePiece
				id="battania_guard_7" />
			<AvailablePiece
				id="battania_guard_8" />
			<AvailablePiece
				id="battania_guard_9" />
			<AvailablePiece
				id="wood_guard_1" />
			<AvailablePiece
				id="axe_craft_1_handle" />
			<AvailablePiece
				id="axe_craft_4_handle" />
			<AvailablePiece
				id="axe_craft_6_handle" />
			<AvailablePiece
				id="axe_craft_7_handle" />
			<AvailablePiece
				id="axe_craft_8_handle" />
			<AvailablePiece
				id="axe_craft_9_handle" />
			<AvailablePiece
				id="axe_craft_10_handle" />
			<AvailablePiece
				id="axe_craft_14_handle" />
			<AvailablePiece
				id="axe_craft_29_handle" />
			<AvailablePiece
				id="axe_craft_30_handle" />
			<AvailablePiece
				id="axe_craft_31_handle" />
			<AvailablePiece
				id="axe_craft_32_handle" />
			<AvailablePiece
				id="empire_pommel_1" />
			<AvailablePiece
				id="empire_pommel_2" />
			<AvailablePiece
				id="empire_pommel_3" />
			<AvailablePiece
				id="empire_pommel_4" />
			<AvailablePiece
				id="empire_pommel_5" />
			<AvailablePiece
				id="empire_pommel_6" />
			<AvailablePiece
				id="sturgian_pommel_1" />
			<AvailablePiece
				id="sturgian_pommel_2" />
			<AvailablePiece
				id="sturgian_pommel_3" />
			<AvailablePiece
				id="sturgian_pommel_4" />
			<AvailablePiece
				id="sturgian_pommel_5" />
			<AvailablePiece
				id="sturgian_pommel_6" />
			<AvailablePiece
				id="sturgian_pommel_7" />
			<AvailablePiece
				id="sturgian_pommel_8" />
			<AvailablePiece
				id="sturgian_pommel_9" />
			<AvailablePiece
				id="sturgian_pommel_10" />
			<AvailablePiece
				id="sturgian_pommel_11" />
			<AvailablePiece
				id="sturgian_pommel_12" />
			<AvailablePiece
				id="khuzait_pommel_1" />
			<AvailablePiece
				id="khuzait_pommel_2" />
			<AvailablePiece
				id="khuzait_pommel_3" />
			<AvailablePiece
				id="khuzait_pommel_4" />
			<AvailablePiece
				id="khuzait_pommel_5" />
			<AvailablePiece
				id="khuzait_pommel_6" />
			<AvailablePiece
				id="khuzait_pommel_7" />
			<AvailablePiece
				id="vlandian_pommel_1" />
			<AvailablePiece
				id="vlandian_pommel_2" />
			<AvailablePiece
				id="vlandian_pommel_3" />
			<AvailablePiece
				id="vlandian_pommel_4" />
			<AvailablePiece
				id="vlandian_pommel_5" />
			<AvailablePiece
				id="vlandian_pommel_6" />
			<AvailablePiece
				id="vlandian_pommel_7" />
			<AvailablePiece
				id="vlandian_pommel_8" />
			<AvailablePiece
				id="vlandian_pommel_9" />
			<AvailablePiece
				id="vlandian_pommel_10" />
			<AvailablePiece
				id="cleaver_pommel_1" />
			<AvailablePiece
				id="cleaver_pommel_2" />
			<AvailablePiece
				id="cleaver_pommel_3" />
			<AvailablePiece
				id="cleaver_pommel_4" />
			<AvailablePiece
				id="aserai_pommel_1" />
			<AvailablePiece
				id="aserai_pommel_2" />
			<AvailablePiece
				id="aserai_pommel_3" />
			<AvailablePiece
				id="aserai_pommel_4" />
			<AvailablePiece
				id="aserai_pommel_5" />
			<AvailablePiece
				id="aserai_pommel_6" />
			<AvailablePiece
				id="aserai_pommel_8" />
			<AvailablePiece
				id="aserai_pommel_9" />
			<AvailablePiece
				id="battania_pommel_1" />
			<AvailablePiece
				id="battania_pommel_2" />
			<AvailablePiece
				id="battania_pommel_3" />
			<AvailablePiece
				id="battania_pommel_4" />
			<AvailablePiece
				id="battania_pommel_5" />
			<AvailablePiece
				id="battania_pommel_6" />
			<AvailablePiece
				id="battania_pommel_7" />
			<AvailablePiece
				id="battania_pommel_8" />
			<AvailablePiece
				id="battania_pommel_9" />
			<AvailablePiece
				id="wood_pommel_1" />
			<!-- AvailablePieces,TwoHandedSword -->
		</AvailablePieces>
	</WeaponDescription>
    </xsl:copy>  
  </xsl:template>  
</xsl:stylesheet>